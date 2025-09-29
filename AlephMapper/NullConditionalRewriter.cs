#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

/// <summary>
/// Rewrites null conditional operators based on the specified policy
/// </summary>
internal class NullConditionalRewriter : CSharpSyntaxRewriter
{
    private readonly NullConditionalRewriteSupport _rewriteSupport;
    private readonly SemanticModel _semanticModel;

    public NullConditionalRewriter(NullConditionalRewriteSupport rewriteSupport, SemanticModel semanticModel)
    {
        _rewriteSupport = rewriteSupport;
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        switch (_rewriteSupport)
        {
            case NullConditionalRewriteSupport.Ignore:
                return IgnoreNullConditional(node);
            case NullConditionalRewriteSupport.Rewrite:
                return RewriteNullConditional(node);
            default:
                return base.VisitConditionalAccessExpression(node);
        }
    }

    private SyntaxNode IgnoreNullConditional(ConditionalAccessExpressionSyntax node)
    {
        // Transform A?.B to A.B
        var expression = node.Expression;
        var whenNotNull = node.WhenNotNull;

        // Visit the expression recursively first
        expression = (ExpressionSyntax)Visit(expression);

        if (whenNotNull is MemberBindingExpressionSyntax memberBinding)
        {
            // A?.B becomes A.B
            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                memberBinding.Name);

            return Visit(memberAccess)!;
        }

        if (whenNotNull is ElementBindingExpressionSyntax elementBinding)
        {
            // A?[index] becomes A[index]
            var elementAccess = SyntaxFactory.ElementAccessExpression(
                expression,
                elementBinding.ArgumentList);

            return Visit(elementAccess)!;
        }

        if (whenNotNull is InvocationExpressionSyntax invocationExpression)
        {
            // A?.Method() becomes A.Method()
            // This is more complex as we need to reconstruct the member access
            if (invocationExpression.Expression is MemberBindingExpressionSyntax methodBinding)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression,
                    methodBinding.Name);

                var newInvocation = SyntaxFactory.InvocationExpression(
                    memberAccess,
                    invocationExpression.ArgumentList);

                return Visit(newInvocation)!;
            }
        }

        // Fallback: return the original node if we can't handle it
        return base.VisitConditionalAccessExpression(node) ?? node;
    }

    private SyntaxNode RewriteNullConditional(ConditionalAccessExpressionSyntax node)
    {
        // Transform A?.B to (A != null ? A.B : null) and ALWAYS parenthesize the produced conditional
        var expression = node.Expression;
        var whenNotNull = node.WhenNotNull;

        // Visit the expression recursively first
        expression = (ExpressionSyntax)Visit(expression)!;

        // Create the null check condition: A != null
        var nullCheck = SyntaxFactory.BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            expression,
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

        ExpressionSyntax accessExpression;
        ExpressionSyntax nullValue;

        if (whenNotNull is MemberBindingExpressionSyntax memberBinding)
        {
            // A?.B becomes A.B
            accessExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                memberBinding.Name);

            // Determine the type for the null value
            var typeInfo = _semanticModel.GetTypeInfo(node);
            if (typeInfo.Type?.CanBeReferencedByName == true)
            {
                if (typeInfo.Type.IsValueType && typeInfo.Type.Name != "Nullable")
                {
                    // For value types, use default(T)
                    nullValue = SyntaxFactory.DefaultExpression(
                        SyntaxFactory.IdentifierName(typeInfo.Type.Name));
                }
                else
                {
                    // For reference types and nullable value types, use null
                    nullValue = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
                }
            }
            else
            {
                // Default to null if we can't determine the type
                nullValue = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
            }
        }
        else if (whenNotNull is ElementBindingExpressionSyntax elementBinding)
        {
            // A?[index] becomes A[index]
            accessExpression = SyntaxFactory.ElementAccessExpression(
                expression,
                elementBinding.ArgumentList);
            nullValue = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        }
        else if (whenNotNull is InvocationExpressionSyntax invocationExpression &&
                 invocationExpression.Expression is MemberBindingExpressionSyntax methodBinding)
        {
            // A?.Method() becomes A.Method()
            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                methodBinding.Name);

            accessExpression = SyntaxFactory.InvocationExpression(
                memberAccess,
                invocationExpression.ArgumentList);
            nullValue = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        }
        else
        {
            // Fallback: return the original node if we can't handle it
            return base.VisitConditionalAccessExpression(node) ?? node;
        }

        // Create the conditional expression: A != null ? A.B : null
        var conditionalExpression = SyntaxFactory.ConditionalExpression(
            nullCheck,
            (Visit(accessExpression) as ExpressionSyntax) ?? accessExpression,
            nullValue);

        // Always wrap in parentheses to preserve original operator precedence when substituted
        return SyntaxFactory.ParenthesizedExpression(conditionalExpression)
                             .WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        // Handle chained null conditional expressions like A?.B?.C
        var left = Visit(node.Left) as ExpressionSyntax ?? node.Left;
        var right = Visit(node.Right) as ExpressionSyntax ?? node.Right;

        if (left != node.Left || right != node.Right)
        {
            return node.WithLeft(left).WithRight(right);
        }

        return base.VisitBinaryExpression(node);
    }

    static bool ConditionalNeedsParenthesesInBinaryExpression(SyntaxToken operatorToken)
    {
        // Conditional operator (?:) has lower precedence than most operators
        // It needs parentheses when used with operators that have higher precedence

        // These operators have HIGHER precedence than conditional, so parentheses ARE needed:
        return operatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||    // ==
               operatorToken.IsKind(SyntaxKind.ExclamationEqualsToken) || // !=
               operatorToken.IsKind(SyntaxKind.LessThanToken) ||          // <
               operatorToken.IsKind(SyntaxKind.LessThanEqualsToken) ||    // <=
               operatorToken.IsKind(SyntaxKind.GreaterThanToken) ||       // >
               operatorToken.IsKind(SyntaxKind.GreaterThanEqualsToken) || // >=
               operatorToken.IsKind(SyntaxKind.PlusToken) ||              // +
               operatorToken.IsKind(SyntaxKind.MinusToken) ||             // -
               operatorToken.IsKind(SyntaxKind.AsteriskToken) ||          // *
               operatorToken.IsKind(SyntaxKind.SlashToken) ||             // /
               operatorToken.IsKind(SyntaxKind.PercentToken) ||           // %
               operatorToken.IsKind(SyntaxKind.QuestionQuestionToken);    // ?? - THIS WAS THE MISSING PIECE!
    }
}
#nullable restore