#nullable enable
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        if (forUpdateMethod)
        {
            // fix missing leading trivia on 'switch' keyword
            var switchExpression = (SwitchExpressionSyntax?)base.VisitSwitchExpression(node);
            return switchExpression?.WithSwitchKeyword(node.SwitchKeyword.WithLeadingTrivia(Space));
        }

        // Reverse arms order to start from the default value
        var arms = node.Arms.Reverse();

        ExpressionSyntax? currentExpression = null;

        foreach (var arm in arms)
        {
            var armExpression = VisitClean(arm.Expression);
            var governingExpressionNode = node.GoverningExpression.WithoutTrivia();
            var governingExpression = VisitClean(governingExpressionNode);

            // Handle fallback value
            if (currentExpression == null)
            {
                currentExpression = arm.Pattern is DiscardPatternSyntax
                    ? armExpression
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                continue;
            }

            // Handle each arm, only if it's a constant expression
            if (arm.Pattern is ConstantPatternSyntax constant)
            {
                var constantExpression = VisitClean(constant.Expression);
                ExpressionSyntax expression = PadBinaryOperator(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        governingExpression,
                        constantExpression));

                // Add when clause as a AND expression
                if (arm.WhenClause != null)
                {
                    expression = BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        expression,
                        VisitClean(arm.WhenClause.Condition)
                    );
                }

                currentExpression = ConditionalExpression(
                    expression,
                    armExpression,
                    currentExpression
                );

                continue;
            }

            if (arm.Pattern is DeclarationPatternSyntax declaration)
            {
                var getTypeExpression = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    governingExpression,
                    IdentifierName("GetType"));

                var getTypeCall = InvocationExpression(getTypeExpression);
                var typeofExpression = TypeOfExpression(declaration.Type);
                var equalsExpression = PadBinaryOperator(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        getTypeCall,
                        typeofExpression));

                ExpressionSyntax condition = equalsExpression;
                if (arm.WhenClause != null)
                {
                    condition = BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        equalsExpression,
                        VisitClean(arm.WhenClause.Condition)
                    );
                }

                var modifiedArmExpression = ReplaceVariableWithCast(
                    armExpression,
                    declaration,
                    governingExpressionNode);
                currentExpression = ConditionalExpression(
                    condition,
                    modifiedArmExpression,
                    currentExpression
                );

                continue;
            }

            throw new InvalidOperationException(
                $"Switch expressions rewriting supports only constant values and declaration patterns (Type var). " +
                $"Unsupported pattern: {arm.Pattern.GetType().Name}"
            );
        }

        return currentExpression;
    }

    private ExpressionSyntax ReplaceVariableWithCast(ExpressionSyntax expression, DeclarationPatternSyntax declaration,
        ExpressionSyntax governingExpression)
    {
        if (declaration.Designation is SingleVariableDesignationSyntax variableDesignation)
        {
            var variableName = variableDesignation.Identifier.ValueText;

            var castExpression = ParenthesizedExpression(
                CastExpression(
                    declaration.Type,
                    (ExpressionSyntax)Visit(governingExpression)
                )
            );

            var rewriter = new ParameterSubstitutionRewriter(variableName, castExpression);
            return (ExpressionSyntax)rewriter.Visit(expression);
        }

        return expression;
    }

    private ExpressionSyntax VisitClean(ExpressionSyntax expression)
    {
        var stripped = expression.WithoutTrivia();
        var rewritten = (ExpressionSyntax?)Visit(stripped);
        return (rewritten ?? stripped).WithoutTrivia();
    }

    private static BinaryExpressionSyntax PadBinaryOperator(BinaryExpressionSyntax expression)
    {
        var token = expression.OperatorToken;
        return expression.WithOperatorToken(
            Token(
                TriviaList(Space),
                token.Kind(),
                TriviaList(Space)));
    }
}

#nullable restore
