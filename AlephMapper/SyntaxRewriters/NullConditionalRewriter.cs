#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

/// <summary>
/// Rewrites null conditional operators based on the specified policy, need to be applied to expression only.
/// </summary>
internal class NullConditionalRewriter(NullConditionalRewrite rewriteSupport) : CSharpSyntaxRewriter
{
    private readonly Stack<ExpressionSyntax> _conditionalAccessExpressionsStack = new();

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None)
        {
            return base.VisitConditionalAccessExpression(node);
        }

        var targetExpression = (ExpressionSyntax)Visit(node.Expression);

        _conditionalAccessExpressionsStack.Push(targetExpression);

        if (rewriteSupport is NullConditionalRewrite.Ignore)
        {
            // Ignore the conditional access and simply visit the WhenNotNull expression
            return Visit(node.WhenNotNull);
        }

        if (rewriteSupport is NullConditionalRewrite.Rewrite)
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
                            (ExpressionSyntax)Visit(node.WhenNotNull).WithoutTrivia()
                        ).WithLeadingTrivia(Space)
                        .WithTrailingTrivia(Space),
                    LiteralExpression(SyntaxKind.NullLiteralExpression)
                        .WithLeadingTrivia(Space)
                )
            );
        }

        return base.VisitConditionalAccessExpression(node);

    }

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (rewriteSupport == NullConditionalRewrite.None || _conditionalAccessExpressionsStack.Count == 0)
        {
            return base.VisitMemberBindingExpression(node);
        }

        var targetExpression = _conditionalAccessExpressionsStack.Pop();

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

        var targetExpression = _conditionalAccessExpressionsStack.Pop();

        return rewriteSupport switch
        {
            NullConditionalRewrite.Ignore => ElementAccessExpression(targetExpression, node.ArgumentList),
            NullConditionalRewrite.Rewrite => ElementAccessExpression(targetExpression, node.ArgumentList),
            _ => throw new ArgumentOutOfRangeException(nameof(rewriteSupport), rewriteSupport, null)
        };
    }
}
#nullable restore