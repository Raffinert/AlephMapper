using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AlephMapper.Models;

internal sealed class MappingModel(
    INamedTypeSymbol containingType,
    IMethodSymbol methodSymbol,
    string name,
    string paramName,
    ITypeSymbol paramType,
    ITypeSymbol returnType,
    ArrowExpressionClauseSyntax bodySyntax,
    SemanticModel semanticModel,
    bool isExpressive,
    bool isUpdatable,
    bool classIsStaticAndPartial,
    NullConditionalRewrite nullStrategy,
    CollectionPropertiesPolicy collectionPolicy,
    IReadOnlyList<string> usingDirectives)
{
    public readonly INamedTypeSymbol ContainingType = containingType;
    public readonly IMethodSymbol MethodSymbol = methodSymbol;
    public readonly string Name = name;
    public readonly string ParamName = paramName;
    public readonly ITypeSymbol ParamType = paramType;
    public readonly ITypeSymbol ReturnType = returnType;
    public readonly ArrowExpressionClauseSyntax BodySyntax = bodySyntax;
    public readonly SemanticModel SemanticModel = semanticModel;

    public readonly bool IsExpressive = isExpressive;
    public readonly bool IsUpdatable = isUpdatable;
    public readonly bool IsClassPartial = classIsStaticAndPartial;

    public readonly NullConditionalRewrite NullStrategy = nullStrategy;
    public readonly CollectionPropertiesPolicy CollectionPolicy = collectionPolicy;
    public readonly IReadOnlyList<string> UsingDirectives = usingDirectives;

    public override bool Equals(object obj)
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