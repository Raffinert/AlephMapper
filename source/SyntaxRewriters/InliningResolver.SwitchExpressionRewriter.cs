#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
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
            var lowered = LowerPattern(arm.Pattern, governing);
            if (lowered is null)
            {
                _unsupportedSwitchPatterns.Add(new UnsupportedSwitchPatternInfo(arm.Pattern));
                return node;
            }

            var condition = lowered.Condition;
            var pushedPatternVariables = 0;
            try
            {
                foreach (var binding in lowered.Bindings)
                {
                    _switchPatternVariables.Push((binding.Key, binding.Value));
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

    private sealed class PatternLoweringResult(ExpressionSyntax condition,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax> bindings)
    {
        public ExpressionSyntax Condition { get; } = condition;
        public IReadOnlyDictionary<ISymbol, ExpressionSyntax> Bindings { get; } = bindings;
    }

    private PatternLoweringResult? LowerPattern(PatternSyntax pattern, ExpressionSyntax input)
    {
        var bindings = new Dictionary<ISymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);
        var condition = ConvertPattern(pattern, input, bindings);
        return condition is null ? null : new PatternLoweringResult(condition, bindings);
    }

    private ExpressionSyntax? ConvertDeclaration(DeclarationPatternSyntax pattern, ExpressionSyntax input,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        if (pattern.Designation is SingleVariableDesignationSyntax designation)
        {
            if (!CanQuerySemanticModel(designation) || model.GetDeclaredSymbol(designation) is not ILocalSymbol local)
                return null;
            bindings.Add(local, ParenthesizedExpression(CastExpression(pattern.Type, input)));
        }
        return PadBinaryOperator(BinaryExpression(SyntaxKind.IsExpression, input, pattern.Type));
    }

    private ExpressionSyntax? ConvertPattern(PatternSyntax pattern, ExpressionSyntax governing,
        Dictionary<ISymbol, ExpressionSyntax> bindings) =>
        pattern switch
        {
            ConstantPatternSyntax constant => ConvertConstantPattern(constant, governing),

            // 'is T' preserves inheritance and handles null, unlike GetType() == typeof(T).
            DeclarationPatternSyntax declaration => ConvertDeclaration(declaration, governing, bindings),

            TypePatternSyntax type => PadBinaryOperator(BinaryExpression(
                SyntaxKind.IsExpression, governing, type.Type)),

            VarPatternSyntax variable => BindDesignation(variable.Designation, governing, bindings)
                ? LiteralExpression(SyntaxKind.TrueLiteralExpression) : null,

            ListPatternSyntax list => ConvertList(list, governing, bindings),

            RecursivePatternSyntax recursive => ConvertRecursive(recursive, governing, bindings),

            RelationalPatternSyntax relational => ConvertRelationalPattern(relational, governing),

            ParenthesizedPatternSyntax parenthesized =>
                ConvertPattern(parenthesized.Pattern, governing, bindings),

            BinaryPatternSyntax binary when
                binary.OperatorToken.IsKind(SyntaxKind.AndKeyword) ||
                binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) =>
                ConvertBinaryPattern(binary, governing, bindings),

            UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) =>
                ConvertNotPattern(unary, governing, bindings),

            DiscardPatternSyntax => LiteralExpression(SyntaxKind.TrueLiteralExpression),

            _ => null
        };

    private bool BindDesignation(VariableDesignationSyntax? designation, ExpressionSyntax input,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        if (designation is null or DiscardDesignationSyntax) return true;
        if (designation is not SingleVariableDesignationSyntax single ||
            !CanQuerySemanticModel(single) || model.GetDeclaredSymbol(single) is not ILocalSymbol local)
            return false;
        bindings.Add(local, input);
        return true;
    }

    private static ExpressionSyntax And(ExpressionSyntax left, ExpressionSyntax right) =>
        ParenthesizedExpression(PadBinaryOperator(BinaryExpression(SyntaxKind.LogicalAndExpression, left, right)));

    private static ExpressionSyntax NonNull(ExpressionSyntax input) =>
        PadBinaryOperator(BinaryExpression(SyntaxKind.NotEqualsExpression,
            ParenthesizedExpression(CastExpression(PredefinedType(Token(SyntaxKind.ObjectKeyword)), input)),
            LiteralExpression(SyntaxKind.NullLiteralExpression)));

    private ExpressionSyntax? ConvertRecursive(RecursivePatternSyntax pattern, ExpressionSyntax input,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        if (!CanQuerySemanticModel(pattern) || model.GetOperation(pattern) is not IRecursivePatternOperation operation)
            return null;
        // Positional lowering is handled separately, only for verified synthesized record members.
        if (pattern.PositionalPatternClause is not null) return null;
        return ConvertRecursiveOperation(operation, pattern.Type, pattern.Designation, input, bindings);
    }

    private ExpressionSyntax? ConvertRecursiveOperation(IRecursivePatternOperation operation, TypeSyntax? type,
        VariableDesignationSyntax? designation, ExpressionSyntax input, Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        var matched = input;
        ExpressionSyntax condition;
        if (type is not null)
        {
            condition = PadBinaryOperator(BinaryExpression(SyntaxKind.IsExpression, input, type));
            matched = ParenthesizedExpression(CastExpression(type, input));
        }
        else condition = NonNull(input);
        if (operation.MatchedType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            matched = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, matched, IdentifierName("Value"));
        foreach (var property in operation.PropertySubpatterns)
        {
            var path = PropertyPath(property.Member, matched, ref condition);
            if (path is null) return null;
            var lowered = property.Pattern is IRecursivePatternOperation implicitRecursive && property.Pattern.Syntax is not RecursivePatternSyntax
                ? ConvertRecursiveOperation(implicitRecursive, null, null, path, bindings)
                : property.Pattern.Syntax is PatternSyntax nested ? ConvertPattern(nested, path, bindings) : null;
            if (lowered is null) return null;
            condition = And(condition, lowered);
        }
        return BindDesignation(designation, matched, bindings) ? condition : null;
    }

    // The bound operation supplies extended member chains, including inherited and
    // hidden members. Each intermediate receiver is guarded before its next access.
    private ExpressionSyntax? PropertyPath(IOperation operation, ExpressionSyntax root, ref ExpressionSyntax condition)
    {
        ISymbol symbol;
        IOperation? instance;
        switch (operation)
        {
            case IPropertyReferenceOperation property when !property.Property.IsIndexer && property.Property.GetMethod is not null:
                symbol = property.Property; instance = property.Instance; break;
            case IFieldReferenceOperation field:
                symbol = field.Field; instance = field.Instance; break;
            default: return null;
        }
        var receiver = root;
        if (instance is not null && instance is not IInstanceReferenceOperation)
        {
            receiver = PropertyPath(instance, root, ref condition);
            if (receiver is null) return null;
            condition = And(condition, NonNull(receiver));
        }
        return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver,
            IdentifierName(Identifier("@" + symbol.Name)));
    }

    private ExpressionSyntax? ConvertList(ListPatternSyntax pattern, ExpressionSyntax input,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        if (!CanQuerySemanticModel(pattern) || model.GetOperation(pattern) is not IListPatternOperation operation)
            return null;
        var isArray = operation.InputType is IArrayTypeSymbol { Rank: 1, IsSZArray: true };
        var isList = operation.InputType is INamedTypeSymbol named && named.OriginalDefinition.Name == "List" &&
            named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            operation.LengthSymbol is IPropertySymbol { Name: "Count" } &&
            operation.IndexerSymbol is IPropertySymbol indexer && indexer.Parameters.Length == 1 &&
            indexer.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
        if (!isArray && !isList) return null;
        var sliceIndex = -1;
        for (var i = 0; i < pattern.Patterns.Count; i++)
        {
            if (pattern.Patterns[i] is not SlicePatternSyntax slice) continue;
            if (sliceIndex >= 0 || slice.Pattern is not null) return null;
            sliceIndex = i;
        }
        var length = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, input,
            IdentifierName(isArray ? "Length" : "Count"));
        var fixedCount = pattern.Patterns.Count - (sliceIndex >= 0 ? 1 : 0);
        var condition = And(NonNull(input), PadBinaryOperator(BinaryExpression(
            sliceIndex >= 0 ? SyntaxKind.GreaterThanOrEqualExpression : SyntaxKind.EqualsExpression,
            length, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(fixedCount)))));
        for (var i = 0; i < pattern.Patterns.Count; i++)
        {
            if (i == sliceIndex) continue;
            ExpressionSyntax index = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i));
            if (sliceIndex >= 0 && i > sliceIndex)
                index = PadBinaryOperator(BinaryExpression(SyntaxKind.SubtractExpression, length,
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(pattern.Patterns.Count - i))));
            var element = ElementAccessExpression(input,
                BracketedArgumentList(SingletonSeparatedList(Argument(index))));
            var lowered = ConvertPattern(pattern.Patterns[i], element, bindings);
            if (lowered is null) return null;
            condition = And(condition, lowered);
        }
        return BindDesignation(pattern.Designation, input, bindings) ? condition : null;
    }

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

        // A type-only pattern may be parsed as a constant until semantic binding.
        if (CanQuerySemanticModel(constant.Expression) && model.GetSymbolInfo(constant.Expression).Symbol is ITypeSymbol type)
            return PadBinaryOperator(BinaryExpression(SyntaxKind.IsExpression, governing,
                ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));

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

    private ExpressionSyntax? ConvertBinaryPattern(BinaryPatternSyntax binary, ExpressionSyntax governing,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        var left = ConvertPattern(binary.Left, governing, bindings);
        var right = ConvertPattern(binary.Right, governing, bindings);
        if (left is null || right is null)
            return null;

        var kind = binary.OperatorToken.IsKind(SyntaxKind.AndKeyword)
            ? SyntaxKind.LogicalAndExpression
            : SyntaxKind.LogicalOrExpression;
        return ParenthesizedExpression(PadBinaryOperator(BinaryExpression(kind, left, right)));
    }

    private ExpressionSyntax? ConvertNotPattern(UnaryPatternSyntax unary, ExpressionSyntax governing,
        Dictionary<ISymbol, ExpressionSyntax> bindings)
    {
        var operand = ConvertPattern(unary.Pattern, governing, bindings);
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
