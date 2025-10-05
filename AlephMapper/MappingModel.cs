using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

internal sealed class MappingModel
{
    public readonly INamedTypeSymbol ContainingType;
    public readonly IMethodSymbol MethodSymbol;
    public readonly string Name;
    public readonly string ParamName;
    public readonly ITypeSymbol ParamType;
    public readonly ITypeSymbol ReturnType;
    public readonly ExpressionSyntax BodySyntax;
    public readonly SemanticModel SemanticModel;

    public readonly bool IsExpressive;
    public readonly bool IsUpdateable;
    public readonly bool IsClassPartial;

    public readonly NullConditionalRewrite NullStrategy; 

    public MappingModel(INamedTypeSymbol containingType,
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
        ContainingType = containingType;
        MethodSymbol = methodSymbol;
        Name = name;
        ParamName = paramName;
        ParamType = paramType;
        ReturnType = returnType;
        BodySyntax = bodySyntax;
        SemanticModel = semanticModel;
        IsExpressive = isExpressive;
        IsUpdateable = isUpdateable;
        IsClassPartial = classIsStaticAndPartial;
        NullStrategy = nullStrategy;
    }

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