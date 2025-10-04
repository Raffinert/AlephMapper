#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AlephMapper;

/// <summary>
/// Rewrites null conditional operators based on the specified policy
/// </summary>
internal class NullConditionalRewriter(NullConditionalRewrite rewriteSupport) : CSharpSyntaxRewriter
{
    private readonly Stack<ExpressionSyntax> _conditionalAccessExpressionsStack = new();

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        var targetExpression = (ExpressionSyntax)Visit(node.Expression);

        _conditionalAccessExpressionsStack.Push(targetExpression);

        if (rewriteSupport == NullConditionalRewrite.None)
        {
            //var diagnostic = Diagnostic.Create(Diagnostics.NullConditionalRewriteUnsupported, node.GetLocation(), node);
            //_context.ReportDiagnostic(diagnostic);

            // Return the original node, do not attempt further rewrites
            return node;
        }

        if (rewriteSupport is NullConditionalRewrite.Ignore)
        {
            // Ignore the conditional access and simply visit the WhenNotNull expression
            return Visit(node.WhenNotNull);
        }

        if (rewriteSupport is NullConditionalRewrite.Rewrite)
        {
            return SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.ConditionalExpression(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        targetExpression.WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                            .WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                    ),
                    SyntaxFactory.ParenthesizedExpression(
                            (ExpressionSyntax)Visit(node.WhenNotNull)
                        ).WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                        .WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                        .WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                )
            );
        }

        return base.VisitConditionalAccessExpression(node);

    }

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (_conditionalAccessExpressionsStack.Count > 0)
        {
            var targetExpression = _conditionalAccessExpressionsStack.Pop();

            return rewriteSupport switch
            {
                NullConditionalRewrite.Ignore => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
                NullConditionalRewrite.Rewrite => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, node.Name),
                _ => node
            };
        }

        return base.VisitMemberBindingExpression(node);
    }

    public override SyntaxNode? VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        if (_conditionalAccessExpressionsStack.Count > 0)
        {
            var targetExpression = _conditionalAccessExpressionsStack.Pop();

            return rewriteSupport switch
            {
                NullConditionalRewrite.Ignore => SyntaxFactory.ElementAccessExpression(targetExpression, node.ArgumentList),
                NullConditionalRewrite.Rewrite => SyntaxFactory.ElementAccessExpression(targetExpression, node.ArgumentList),
                _ => Visit(node)
            };
        }

        return base.VisitElementBindingExpression(node);
    }
}
#nullable restore