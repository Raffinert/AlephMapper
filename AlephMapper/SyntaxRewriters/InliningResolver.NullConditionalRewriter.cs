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

            var rewrittenWhenNotNull = (ExpressionSyntax)Visit(node.WhenNotNull);
            
            ////todo: tech debt. Now it patches for wrongly substituted expression that misses first part
            if (FirstChar(rewrittenWhenNotNull) == '.')
            {
                rewrittenWhenNotNull = ParseExpression($"{targetExpression}{rewrittenWhenNotNull}");
            }

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

    private static char FirstChar(ExpressionSyntax expr)
    {
        if (expr.Span.IsEmpty) return 'a';

        var text = expr.SyntaxTree.GetText();
        return text[expr.SpanStart];
    }

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitMemberBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Peek();

        return rewriteSupport switch
        {
            NullConditionalRewrite.Ignore => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
            NullConditionalRewrite.Rewrite => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(rewriteSupport), rewriteSupport, null)
        };
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