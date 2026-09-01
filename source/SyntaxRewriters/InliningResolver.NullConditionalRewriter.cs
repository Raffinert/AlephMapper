#nullable enable
using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

/// <summary>
/// Rewrites null-conditional operators based on the specified policy. Apply to expressions only.
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
                if (!forUpdateMethod)
                {
                    _unsupportedNullConditionals.Add(new UnsupportedNullConditionalInfo(node));
                }

                _conditionalAccessExpressionsStack.Push(node.Expression);
                var rewritten = (ExpressionSyntax)base.VisitConditionalAccessExpression(node)!;
                var annotated = rewritten.GetAnnotatedNodes(InlinedConditionalAnnotation).ToArray();
                if (annotated.FirstOrDefault() is { } ann) return ann;
                return rewritten;
            }

            var targetExpression = (ExpressionSyntax)Visit(node.Expression);
            if (rewriteSupport == NullConditionalRewrite.Rewrite &&
                !CanSafelyRepeat(targetExpression))
            {
                _unsafeConditionalReceivers.Add(new UnsafeConditionalReceiverInfo(node.Expression));
            }

            _conditionalAccessExpressionsStack.Push(targetExpression);

            var rewrittenWhenNotNull = (ExpressionSyntax)Visit(node.WhenNotNull);
            rewrittenWhenNotNull = CreateMemberAccessFromDotPrefixedExpression(targetExpression, rewrittenWhenNotNull);

            if (rewriteSupport is NullConditionalRewrite.Ignore)
            {
                // Ignore the conditional access and simply return the accessed expression
                // Preserve the source semantics while suppressing the nullable-flow warning
                // introduced by replacing `?.` with a regular member access.
                if (!nullablePolicy.WarningsEnabled)
                {
                    return rewrittenWhenNotNull;
                }

                return rewrittenWhenNotNull is PostfixUnaryExpressionSyntax postfix &&
                    postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                    ? rewrittenWhenNotNull
                    : PostfixUnaryExpression(
                        SyntaxKind.SuppressNullableWarningExpression,
                        rewrittenWhenNotNull);
            }

            if (rewriteSupport is NullConditionalRewrite.Rewrite)
            {
                var typeInfo = model.GetTypeInfo(node);
                var convertedType = typeInfo.ConvertedType ?? typeInfo.Type;

                if (convertedType is not null)
                {
                    // A null-conditional access always produces null when its receiver is null.
                    // Its converted type can still be non-nullable when it is immediately consumed
                    // by an expression such as `??`, so explicitly retain that nullability here.
                    var castTypeName = TypeDisplay.ForSymbol(
                        convertedType,
                        NullableAnnotation.Annotated,
                        nullablePolicy);

                    var nullValue = convertedType.IsReferenceType &&
                        convertedType.NullableAnnotation != NullableAnnotation.Annotated &&
                        nullablePolicy.WarningsEnabled
                        ? (ExpressionSyntax)PostfixUnaryExpression(
                            SyntaxKind.SuppressNullableWarningExpression,
                            LiteralExpression(SyntaxKind.NullLiteralExpression))
                        : LiteralExpression(SyntaxKind.NullLiteralExpression);
                    var nullBranch = CastExpression(
                        ParseTypeName(convertedType.IsReferenceType &&
                                      convertedType.NullableAnnotation != NullableAnnotation.Annotated
                            ? TypeDisplay.ForSymbol(
                                convertedType,
                                NullableAnnotation.NotAnnotated,
                                nullablePolicy)
                            : castTypeName),
                        nullValue);

                    return ParenthesizedExpression(
                        ConditionalExpression(
                            BinaryExpression(
                                SyntaxKind.NotEqualsExpression,
                                targetExpression.WithoutTrivia().WithTrailingTrivia(Space),
                                LiteralExpression(SyntaxKind.NullLiteralExpression).WithLeadingTrivia(Space)
                            ),
                            ParenthesizedExpression(rewrittenWhenNotNull.WithoutTrivia())
                                .WithLeadingTrivia(Space)
                                .WithTrailingTrivia(Space),
                            nullBranch.WithLeadingTrivia(Space)
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

    private static bool CanSafelyRepeat(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax => true,
            ThisExpressionSyntax => true,
            BaseExpressionSyntax => true,
            MemberAccessExpressionSyntax memberAccess => CanSafelyRepeat(memberAccess.Expression),
            MemberBindingExpressionSyntax => true,
            ParenthesizedExpressionSyntax parenthesized => CanSafelyRepeat(parenthesized.Expression),
            CastExpressionSyntax cast => CanSafelyRepeat(cast.Expression),
            PostfixUnaryExpressionSyntax postfix
                when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression) =>
                    CanSafelyRepeat(postfix.Operand),
            ConditionalAccessExpressionSyntax conditional =>
                CanSafelyRepeat(conditional.Expression) &&
                CanSafelyRepeat(conditional.WhenNotNull),
            _ => false
        };
    }

    /// <summary>
    /// Reconstructs member/element/Invocation access off a ConditionalAccess target into normal access.
    /// </summary>
    private static ExpressionSyntax CreateMemberAccessFromDotPrefixedExpression(
        ExpressionSyntax targetExpression,
        ExpressionSyntax whenNotNullExpression)
    {
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
                        return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, left, ma.Name);
                    }

                case ElementAccessExpressionSyntax ea:
                    {
                        var left = Transform(ea.Expression, currentTarget);
                        return ElementAccessExpression(left, ea.ArgumentList);
                    }

                default:
                    return expr;
            }
        }

        return Transform(whenNotNullExpression, targetExpression);
    }

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitMemberBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Peek();
        var memberAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name);
        return memberAccess.WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitElementBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Peek();
        return ElementAccessExpression(targetExpression, node.ArgumentList);
    }

    // Binary expressions are handled in the CollectionExpressionRewriter partial to avoid duplication.
}
#nullable restore
