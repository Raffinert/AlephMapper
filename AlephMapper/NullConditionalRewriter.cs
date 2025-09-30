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
        expression = (ExpressionSyntax)Visit(expression)!;

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
        // Transform A?.B to (A != null ? A.B : null/default)
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
        ExpressionSyntax nullValue = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

        if (whenNotNull is MemberBindingExpressionSyntax memberBinding)
        {
            // A?.B becomes (A != null ? A.B : null)
            accessExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                memberBinding.Name);
        }
        else if (whenNotNull is ElementBindingExpressionSyntax elementBinding)
        {
            // A?[index] becomes (A != null ? A[index] : null)
            accessExpression = SyntaxFactory.ElementAccessExpression(
                expression,
                elementBinding.ArgumentList);
        }
        else if (whenNotNull is InvocationExpressionSyntax invocationExpression &&
                 invocationExpression.Expression is MemberBindingExpressionSyntax methodBinding)
        {
            // A?.Method() becomes (A != null ? A.Method() : null)
            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                methodBinding.Name);

            accessExpression = SyntaxFactory.InvocationExpression(
                memberAccess,
                invocationExpression.ArgumentList);
        }
        else
        {
            // Fallback: return the original node if we can't handle it
            return base.VisitConditionalAccessExpression(node) ?? node;
        }

        // Recursively process the access expression to handle nested null conditionals
        accessExpression = (ExpressionSyntax)Visit(accessExpression);

        // Create the conditional expression: A != null ? A.B : null
        var conditionalExpression = SyntaxFactory.ConditionalExpression(
            nullCheck,
            accessExpression,
            nullValue);

        // Always wrap in parentheses to preserve original operator precedence when substituted
        return SyntaxFactory.ParenthesizedExpression(conditionalExpression)
                             .WithTriviaFrom(node);
    }

    private ExpressionSyntax CreateNullValue(ITypeSymbol? type)
    {
        // For null conditional operators, we always return null
        // The C# null conditional operator always produces a nullable result
        return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
    }

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        // Handle null coalescing with conditional access: A?.B ?? "default"
        if (node.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken))
        {
            // Visit left side first (this will handle any null conditionals)
            var left = Visit(node.Left) as ExpressionSyntax ?? node.Left;
            var right = Visit(node.Right) as ExpressionSyntax ?? node.Right;

            // If the left side was rewritten as a conditional expression, we need to be careful
            // about operator precedence
            if (left is ParenthesizedExpressionSyntax parenthesized &&
                parenthesized.Expression is ConditionalExpressionSyntax conditional)
            {
                // Transform (A != null ? A.B : null) ?? "default" 
                // to (A != null ? A.B : "default")
                var newConditional = SyntaxFactory.ConditionalExpression(
                    conditional.Condition,
                    conditional.WhenTrue,
                    right);

                return SyntaxFactory.ParenthesizedExpression(newConditional);
            }

            return node.WithLeft(left).WithRight(right);
        }

        // Handle other binary expressions normally
        var leftResult = Visit(node.Left) as ExpressionSyntax ?? node.Left;
        var rightResult = Visit(node.Right) as ExpressionSyntax ?? node.Right;

        if (leftResult != node.Left || rightResult != node.Right)
        {
            return node.WithLeft(leftResult).WithRight(rightResult);
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
               operatorToken.IsKind(SyntaxKind.QuestionQuestionToken);    // ??
    }
}
#nullable restore