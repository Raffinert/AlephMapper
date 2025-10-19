using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

internal sealed class MappingModel(
    INamedTypeSymbol containingType,
    IMethodSymbol methodSymbol,
    string name,
    string paramName,
    ITypeSymbol paramType,
    ITypeSymbol returnType,
    ExpressionSyntax bodySyntax,
    SemanticModel semanticModel,
    bool isExpressive,
    bool isUpdateable,
    bool classIsStaticAndPartial,
    NullConditionalRewrite nullStrategy)
{
    public readonly INamedTypeSymbol ContainingType = containingType;
    public readonly IMethodSymbol MethodSymbol = methodSymbol;
    public readonly string Name = name;
    public readonly string ParamName = paramName;
    public readonly ITypeSymbol ParamType = paramType;
    public readonly ITypeSymbol ReturnType = returnType;
    public readonly ExpressionSyntax BodySyntax = bodySyntax;
    public readonly SemanticModel SemanticModel = semanticModel;

    public readonly bool IsExpressive = isExpressive;
    public readonly bool IsUpdateable = isUpdateable;
    public readonly bool IsClassPartial = classIsStaticAndPartial;

    public readonly NullConditionalRewrite NullStrategy = nullStrategy;

    public override bool Equals(object? obj)
    {
        if (obj is MappingModel other)
        {
            return SymbolEqualityComparer.Default.Equals(MethodSymbol, other.MethodSymbol);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(MethodSymbol);
    }
}