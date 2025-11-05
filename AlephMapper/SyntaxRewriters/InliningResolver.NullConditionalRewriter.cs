#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

/// <summary>
/// Rewrites null conditional operators based on the specified policy, need to be applied to expression only.
/// </summary>
internal partial class InliningResolver
{
    private readonly Stack<ExpressionSyntax> _conditionalAccessExpressionsStack = new();

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        try
        {
            if (rewriteSupport == NullConditionalRewrite.None)
            {
                _conditionalAccessExpressionsStack.Push(node.Expression);
                var rewrittenConditional = (ExpressionSyntax)base.VisitConditionalAccessExpression(node)!;
                var annotated = rewrittenConditional.GetAnnotatedNodes("AlephMapper.InlinedConditional").ToArray();

                if (annotated.FirstOrDefault() != null)
                {
                    return annotated[0];
                }

                return rewrittenConditional;
            }

            var targetExpression = (ExpressionSyntax)Visit(node.Expression);

            _conditionalAccessExpressionsStack.Push(targetExpression);

            // FIXED: Process WhenNotNull with proper context to prevent malformed expressions
            var rewrittenWhenNotNull = (ExpressionSyntax)Visit(node.WhenNotNull);
            
            // The root cause is that when MemberBindingExpression gets processed and then inlined/substituted,
            // it can create expressions that start with dots. Reconstruct properly via AST transformation
            // only when the WhenNotNull part actually represents a dot- or element-binding form.
            rewrittenWhenNotNull = CreateMemberAccessFromDotPrefixedExpression(targetExpression, rewrittenWhenNotNull);

            if (rewriteSupport is NullConditionalRewrite.Ignore)
            {
                // Ignore the conditional access and simply visit the WhenNotNull expression
                return rewrittenWhenNotNull;
            }

            if (rewriteSupport is NullConditionalRewrite.Rewrite)
            {
                var typeInfo = model.GetTypeInfo(node);

                // Do not translate until we can resolve the target type
                if (typeInfo.ConvertedType is not null)
                {

                    return ParenthesizedExpression(
                        ConditionalExpression(
                            BinaryExpression(
                                SyntaxKind.NotEqualsExpression,
                                targetExpression.WithoutTrivia().WithTrailingTrivia(Space),
                                LiteralExpression(SyntaxKind.NullLiteralExpression)
                                    .WithLeadingTrivia(Space)
                            ),
                            ParenthesizedExpression(
                                    rewrittenWhenNotNull.WithoutTrivia()
                                ).WithLeadingTrivia(Space)
                                .WithTrailingTrivia(Space),
                            CastExpression(
                                    ParseName(typeInfo.ConvertedType.ToDisplayString(SymbolDisplayFormat
                                        .MinimallyQualifiedFormat)),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression))
                                .WithLeadingTrivia(Space)
                        )
                    );
                }
            }

            return base.VisitConditionalAccessExpression(node);
        }
        finally
        {
            _conditionalAccessExpressionsStack.Pop();
        }
    }

    /// <summary>
    /// Creates a proper member access expression from a target and a dot-prefixed expression.
    /// This replaces the ParseExpression tech debt with proper AST construction.
    /// </summary>
    private static ExpressionSyntax CreateMemberAccessFromDotPrefixedExpression(
        ExpressionSyntax targetExpression,
        ExpressionSyntax whenNotNullExpression)
    {
        // Recursively rewrite any MemberBinding/ElementBinding rooted chain into a proper
        // member/element access expression starting from targetExpression.

        ExpressionSyntax Transform(ExpressionSyntax expr, ExpressionSyntax currentTarget)
        {
            switch (expr)
            {
                case MemberBindingExpressionSyntax mb:
                    return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, currentTarget, mb.Name);

                case ElementBindingExpressionSyntax eb:
                    return ElementAccessExpression(currentTarget, eb.ArgumentList);

                case InvocationExpressionSyntax inv:
                {
                    var newCallee = Transform(inv.Expression, currentTarget);
                    return InvocationExpression(newCallee, inv.ArgumentList);
                }

                case MemberAccessExpressionSyntax ma:
                {
                    var left = Transform(ma.Expression, currentTarget);
                    // Normalize to simple member access
                    return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, left, ma.Name);
                }

                case ElementAccessExpressionSyntax ea:
                {
                    var left = Transform(ea.Expression, currentTarget);
                    return ElementAccessExpression(left, ea.ArgumentList);
                }
            }

            // For other nodes, return as-is
            return expr;
        }

        return Transform(whenNotNullExpression, targetExpression);
    }

    private static bool IsDotOrElementBinding(ExpressionSyntax expr)
    {
        return expr is MemberBindingExpressionSyntax
               || expr is ElementBindingExpressionSyntax
               || expr is InvocationExpressionSyntax inv && inv.Expression is MemberBindingExpressionSyntax
               || expr is ElementAccessExpressionSyntax ea && ea.Expression is MemberBindingExpressionSyntax
               || expr is MemberAccessExpressionSyntax ma && (ma.Expression is MemberBindingExpressionSyntax);
    }

    // Leftover helper removed as reconstruction no longer relies on raw text

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitMemberBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Peek();

        // IMPROVED: Ensure we create proper member access expressions that won't need patching later
        var memberAccess = rewriteSupport switch
        {
            NullConditionalRewrite.Ignore => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
            NullConditionalRewrite.Rewrite => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(rewriteSupport), rewriteSupport, null)
        };

        // Ensure the result maintains proper trivia and doesn't get corrupted during parameter substitution
        return memberAccess.WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitElementBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Peek();

        return rewriteSupport switch
        {
            NullConditionalRewrite.Ignore => ElementAccessExpression(targetExpression, node.ArgumentList),
            NullConditionalRewrite.Rewrite => ElementAccessExpression(targetExpression, node.ArgumentList),
            _ => throw new ArgumentOutOfRangeException(nameof(rewriteSupport), rewriteSupport, null)
        };
    }

    private SyntaxNode? VisitNullRewriterBinaryExpression(BinaryExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None) return base.VisitBinaryExpression(node);

        // Only handle null coalesce operators
        if (node.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken) && rewriteSupport == NullConditionalRewrite.Ignore)
        {
            // Visit the left side first to process any conditional access operators
            var left = (ExpressionSyntax)Visit(node.Left);

            var typeInfo = model.GetTypeInfo(node.Left);

            // Check if the left side is a nullable value type access that no longer needs null coalesce
            if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.IsValueType)
            {
                // Remove the null coalesce operator and just return the left side
                return left.WithTriviaFrom(node);
            }

            return base.VisitBinaryExpression(node);
        }

        // For non-null coalesce binary expressions, use default behavior
        return base.VisitBinaryExpression(node);
    }

}
#nullable restore
