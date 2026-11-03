#nullable enable

using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var member = CanQuerySemanticModel(node) ? model.GetSymbolInfo(node).Symbol : null;
        if (member is IPropertySymbol property && RoslynCompatibility.IsExtensionType(property.ContainingType))
            return RewriteExtensionProperty(node, property, node.Expression);
        if (member is IMethodSymbol method && RoslynCompatibility.IsExtensionType(method.ContainingType) &&
            !(node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node))
            return UnsupportedExtension(node, method);
        if (TryRewriteExpandedDependency(node, member, out var constant)) return constant;

        var rewritten = (MemberAccessExpressionSyntax?)base.VisitMemberAccessExpression(node);
        if (rewritten is null || member is not { IsStatic: true, ContainingType: { } containingType })
            return rewritten;

        var typeName = TypeDisplay.ForSymbol(containingType, containingType.NullableAnnotation, nullablePolicy);
        return QualifyMember(rewritten.Name, ParseTypeName(typeName).WithTriviaFrom(rewritten.Expression))
            .WithTriviaFrom(rewritten);
    }

    private bool TryRewriteExpandedName(SimpleNameSyntax original, SimpleNameSyntax rewritten, ISymbol? symbol,
        out ExpressionSyntax result)
    {
        result = rewritten;
        if (!_extensionExpansion || symbol is not { IsStatic: true, ContainingType: { } owner } ||
            symbol is not (IFieldSymbol or IPropertySymbol or IMethodSymbol) ||
            original.Parent is MemberAccessExpressionSyntax access && access.Name == original)
            return false;
        if (TryRewriteExpandedDependency(original, symbol, out result)) return true;
        result = QualifyMember(rewritten, ParseName(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .WithTriviaFrom(original);
        return true;
    }

    private bool TryRewriteExpandedDependency(SyntaxNode original, ISymbol? symbol, out ExpressionSyntax result)
    {
        result = null!;
        if (!_extensionExpansion || symbol is null) return false;
        if (symbol is IFieldSymbol { IsConst: true } field && field.Type.TypeKind != TypeKind.Enum)
        {
            result = ExtensionConstant(field).WithTriviaFrom(original);
            return true;
        }
        if (_context.OutputType is not null && !model.Compilation.IsSymbolAccessibleWithin(symbol, _context.OutputType))
            _context.UnsupportedExtensions.Add((symbol, "an expanded body references an inaccessible member"));
        return false;
    }

    private static MemberAccessExpressionSyntax QualifyMember(SimpleNameSyntax name, ExpressionSyntax owner) =>
        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, owner, name);

    private static ExpressionSyntax ExtensionConstant(IFieldSymbol field)
    {
        ExpressionSyntax value = field.ConstantValue switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            string text => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(text)),
            char character => LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(character)),
            bool boolean => LiteralExpression(boolean ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            float number when float.IsNaN(number) || float.IsInfinity(number) => ParseExpression(
                "global::System.Single." + (float.IsNaN(number) ? "NaN" : number > 0 ? "PositiveInfinity" : "NegativeInfinity")),
            double number when double.IsNaN(number) || double.IsInfinity(number) => ParseExpression(
                "global::System.Double." + (double.IsNaN(number) ? "NaN" : number > 0 ? "PositiveInfinity" : "NegativeInfinity")),
            float number => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(number)),
            double number => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(number)),
            decimal number => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(number)),
            _ => ParseExpression(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                field.ConstantValue, quoteStrings: true, useHexadecimalNumbers: false)!)
        };
        return ParenthesizedExpression(CastExpression(ParseTypeName(field.Type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat)), ParenthesizedExpression(value)));
    }

}
