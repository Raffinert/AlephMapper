using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AlephMapper.Models;

/// <summary>
/// Compiler-bound analysis state used only within a source-output callback.
/// It is intentionally not retained by the incremental pipeline.
/// </summary>
internal sealed class MappingAnalysis(
    INamedTypeSymbol containingType,
    IMethodSymbol methodSymbol,
    string name,
    IReadOnlyList<IParameterSymbol> parameters,
    ITypeSymbol returnType,
    ArrowExpressionClauseSyntax bodySyntax,
    SemanticModel semanticModel,
    bool isProjectable,
    bool isUpdatable,
    bool classIsStaticAndPartial,
    NullConditionalRewrite nullStrategy,
    CollectionPropertiesPolicy collectionPolicy,
    IReadOnlyList<string> usingDirectives,
    IReadOnlyList<AdaptationAnalysis> adaptations)
{
    public readonly INamedTypeSymbol ContainingType = containingType;
    public readonly IMethodSymbol MethodSymbol = methodSymbol;
    public readonly string Name = name;
    public readonly IReadOnlyList<IParameterSymbol> Parameters = parameters;
    public readonly ITypeSymbol ParamType = parameters[0].Type;
    public readonly ITypeSymbol ReturnType = returnType;
    public readonly ArrowExpressionClauseSyntax BodySyntax = bodySyntax;
    public readonly SemanticModel SemanticModel = semanticModel;

    public readonly bool IsProjectable = isProjectable;
    public readonly bool IsUpdatable = isUpdatable;
    public readonly bool IsClassPartial = classIsStaticAndPartial;

    public readonly NullConditionalRewrite NullStrategy = nullStrategy;
    public readonly CollectionPropertiesPolicy CollectionPolicy = collectionPolicy;
    public readonly IReadOnlyList<string> UsingDirectives = usingDirectives;
    public readonly IReadOnlyList<AdaptationAnalysis> Adaptations = adaptations;
}
