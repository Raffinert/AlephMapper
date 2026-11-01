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
            var original = (SwitchExpressionSyntax?)base.VisitSwitchExpression(node);
            return original?.WithSwitchKeyword(node.SwitchKeyword.WithLeadingTrivia(Space));
        }

        // Unlike a fallback branch, an unmatched C# switch expression throws. We cannot
        // represent that throw in an EF-translatable lambda, so refuse generation.
        if (node.Arms.Count == 0 ||
            node.Arms[node.Arms.Count - 1].Pattern is not DiscardPatternSyntax ||
            node.Arms[node.Arms.Count - 1].WhenClause is not null)
        {
            _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(node));
            return node;
        }

        var switchText = node.ToFullString();
        var multiline = switchText.IndexOf('\n') >= 0 || switchText.IndexOf('\r') >= 0;

        // Visit original bound nodes before stripping trivia; symbol-aware substitutions
        // need access to the original semantic model.
        var governing = VisitClean(node.GoverningExpression);
        ExpressionSyntax currentExpression = VisitClean(node.Arms[node.Arms.Count - 1].Expression);

        foreach (var arm in node.Arms.Reverse().Skip(1))
        {
            var condition = ConvertPattern(arm.Pattern, governing);
            if (condition is null)
            {
                _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(arm.Pattern));
                return node;
            }

            // Rewrite pattern-local references while the original arm is still
            // bound to its SemanticModel. Substituted caller expressions can have
            // the same identifier text, but never the same ILocalSymbol.
            var hasPatternVariable = arm.Pattern is DeclarationPatternSyntax
            {
                Designation: SingleVariableDesignationSyntax patternDesignation
            } && CanQuerySemanticModel(patternDesignation) &&
                model.GetDeclaredSymbol(patternDesignation) is ILocalSymbol;
            if (hasPatternVariable)
            {
                var declaration = (DeclarationPatternSyntax)arm.Pattern;
                var designation = (SingleVariableDesignationSyntax)declaration.Designation;
                var local = (ILocalSymbol)model.GetDeclaredSymbol(designation)!;
                var cast = ParenthesizedExpression(CastExpression(declaration.Type, governing));
                _switchPatternVariables.Push((local, cast));
            }

            try
            {
                var armExpression = VisitClean(arm.Expression);
                if (arm.WhenClause is not null)
                {
                    var guard = VisitClean(arm.WhenClause.Condition);
                    condition = PadBinaryOperator(BinaryExpression(
                        SyntaxKind.LogicalAndExpression, condition, guard));
                }

                currentExpression = CreateConditional(condition, armExpression, currentExpression, multiline);
            }
            finally
            {
                if (hasPatternVariable)
                    _switchPatternVariables.Pop();
            }
        }

        return currentExpression;
    }

    private ExpressionSyntax? ConvertPattern(PatternSyntax pattern, ExpressionSyntax governing) =>
        pattern switch
        {
            ConstantPatternSyntax constant => PadBinaryOperator(BinaryExpression(
                SyntaxKind.EqualsExpression, governing, VisitClean(constant.Expression))),

            // 'is T' preserves inheritance and handles null, unlike GetType() == typeof(T).
            DeclarationPatternSyntax declaration => PadBinaryOperator(BinaryExpression(
                SyntaxKind.IsExpression, governing, declaration.Type)),

            RelationalPatternSyntax relational => ConvertRelationalPattern(relational, governing),

            ParenthesizedPatternSyntax parenthesized =>
                ConvertPattern(parenthesized.Pattern, governing),

            BinaryPatternSyntax binary when
                binary.OperatorToken.IsKind(SyntaxKind.AndKeyword) ||
                binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) =>
                ConvertBinaryPattern(binary, governing),

            UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) =>
                ConvertNotPattern(unary, governing),

            DiscardPatternSyntax => LiteralExpression(SyntaxKind.TrueLiteralExpression),

            _ => null
        };

    private ExpressionSyntax? ConvertBinaryPattern(BinaryPatternSyntax binary, ExpressionSyntax governing)
    {
        var left = ConvertPattern(binary.Left, governing);
        var right = ConvertPattern(binary.Right, governing);
        if (left is null || right is null)
            return null;

        var kind = binary.OperatorToken.IsKind(SyntaxKind.AndKeyword)
            ? SyntaxKind.LogicalAndExpression
            : SyntaxKind.LogicalOrExpression;
        return ParenthesizedExpression(PadBinaryOperator(BinaryExpression(kind, left, right)));
    }

    private ExpressionSyntax? ConvertNotPattern(UnaryPatternSyntax unary, ExpressionSyntax governing)
    {
        var operand = ConvertPattern(unary.Pattern, governing);
        return operand is null ? null
            : PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(operand));
    }

    private ExpressionSyntax? ConvertRelationalPattern(RelationalPatternSyntax relational, ExpressionSyntax governing)
    {
        var kind = relational.OperatorToken.Kind() switch
        {
            SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
            _ => SyntaxKind.None
        };
        return kind == SyntaxKind.None ? null
            : PadBinaryOperator(BinaryExpression(kind, governing, VisitClean(relational.Expression)));
    }

    private ExpressionSyntax VisitClean(ExpressionSyntax expression)
    {
        var rewritten = (ExpressionSyntax?)Visit(expression);
        return (rewritten ?? expression).WithoutTrivia();
    }

    private static ConditionalExpressionSyntax CreateConditional(
        ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse, bool multiline)
    {
        var kind = multiline
            ? GeneratedSyntaxAnnotations.MultilineConditional
            : GeneratedSyntaxAnnotations.SingleLineConditional;
        return ConditionalExpression(condition, whenTrue, whenFalse)
            .WithAdditionalAnnotations(new SyntaxAnnotation(kind));
    }

    private static BinaryExpressionSyntax PadBinaryOperator(BinaryExpressionSyntax expression)
    {
        var token = expression.OperatorToken;
        return expression.WithOperatorToken(Token(TriviaList(Space), token.Kind(), TriviaList(Space)));
    }
}

internal sealed class UnsupportedSwitchPatternInfo(SyntaxNode syntax)
{
    public string Text { get; } = syntax.ToString();
    public Location Location { get; } = syntax.GetLocation();
}
#nullable restore
