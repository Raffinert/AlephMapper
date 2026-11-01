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

        // Pattern matching can narrow an object-typed input for relational
        // comparisons; a plain 'object > int' comparison cannot. Reject until
        // the lowering can introduce the required type tests/conversions.
        var inputType = CanQuerySemanticModel(node.GoverningExpression)
            ? model.GetTypeInfo(node.GoverningExpression).Type
            : null;
        if (inputType is { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType } &&
            node.Arms.Any(arm => arm.Pattern.DescendantNodesAndSelf()
                .OfType<RelationalPatternSyntax>().Any()))
        {
            _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(node));
            return node;
        }

        var switchText = node.ToFullString();
        var multiline = switchText.IndexOf('\n') >= 0 || switchText.IndexOf('\r') >= 0;

        // Visit original bound nodes before stripping trivia; symbol-aware substitutions
        // need access to the original semantic model.
        var governing = VisitClean(node.GoverningExpression);
        // Helper inlining can turn an invocation into a lower-precedence expression.
        // A binary/conditional governing expression must be grouped before being
        // embedded in a pattern comparison, e.g. (a && b) == false, not a && b == false.
        governing = ParenthesizeCompositeExpression(governing);
        ExpressionSyntax currentExpression = VisitClean(node.Arms[node.Arms.Count - 1].Expression);

        foreach (var arm in node.Arms.Reverse().Skip(1))
        {
            var condition = ConvertPattern(arm.Pattern, governing);
            if (condition is null)
            {
                _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(arm.Pattern));
                return node;
            }

            // ConvertPattern accepts declaration patterns inside parentheses and
            // logical wrappers. Bind every declaration-local symbol from the original
            // pattern (not just an arm whose outermost node is a declaration).
            // Identifier spelling is not sufficient: a substituted caller argument
            // can have the same name as the pattern local.
            var pushedPatternVariables = 0;
            try
            {
                foreach (var declaration in arm.Pattern.DescendantNodesAndSelf()
                             .OfType<DeclarationPatternSyntax>())
                {
                    if (declaration.Designation is not SingleVariableDesignationSyntax designation)
                        continue;

                    if (!CanQuerySemanticModel(designation) ||
                        model.GetDeclaredSymbol(designation) is not ILocalSymbol local)
                    {
                        _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(declaration));
                        return node;
                    }

                    var cast = ParenthesizedExpression(CastExpression(declaration.Type, governing));
                    _switchPatternVariables.Push((local, cast));
                    pushedPatternVariables++;
                }


                var armExpression = VisitClean(arm.Expression);
                if (arm.WhenClause is not null)
                {
                    // A helper can inline to an OR/coalescing/conditional expression.
                    // Do not let a low-precedence guard escape the pattern's AND test.
                    var guard = ParenthesizeCompositeExpression(VisitClean(arm.WhenClause.Condition));
                    condition = PadBinaryOperator(BinaryExpression(
                        SyntaxKind.LogicalAndExpression, condition, guard));
                }

                currentExpression = CreateConditional(condition, armExpression, currentExpression, multiline);
            }
            finally
            {
                while (pushedPatternVariables-- > 0)
                    _switchPatternVariables.Pop();
            }
        }

        return currentExpression;
    }

    private ExpressionSyntax? ConvertPattern(PatternSyntax pattern, ExpressionSyntax governing) =>
        pattern switch
        {
            ConstantPatternSyntax constant => ConvertConstantPattern(constant, governing),

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

    private ExpressionSyntax? ConvertConstantPattern(ConstantPatternSyntax constant, ExpressionSyntax governing)
    {
        // C# constant patterns match NaN, whereas == returns false for NaN.
        // An expression-tree comparison using == cannot represent that test
        // without special lowering, so report AM0019 instead of silently
        // reversing 'not NaN' (or skipping a matching NaN arm).
        if (CanQuerySemanticModel(constant.Expression))
        {
            var value = model.GetConstantValue(constant.Expression);
            if (value.HasValue &&
                (value.Value is double d && double.IsNaN(d) ||
                 value.Value is float f && float.IsNaN(f)))
                return null;
        }

        var compared = governing;
        if (constant.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            // A C# null pattern doesn't invoke an overloaded == operator.
            // Compare boxed values using reference equality semantics.
            compared = ParenthesizedExpression(
                CastExpression(PredefinedType(Token(SyntaxKind.ObjectKeyword)), governing));
        }

        return PadBinaryOperator(BinaryExpression(
            SyntaxKind.EqualsExpression, compared, VisitClean(constant.Expression)));
    }

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

    // A lowered helper call can be an entire expression rather than the original
    // invocation. Group it whenever it is embedded as a child of another operator,
    // preserving its original precedence in both switch governing and guard contexts.
    private static ExpressionSyntax ParenthesizeCompositeExpression(ExpressionSyntax expression) =>
        expression is BinaryExpressionSyntax or ConditionalExpressionSyntax or
            AssignmentExpressionSyntax or IsPatternExpressionSyntax or SwitchExpressionSyntax
                ? ParenthesizedExpression(expression)
                : expression;

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
