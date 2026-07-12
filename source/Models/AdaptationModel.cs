#nullable enable

using Microsoft.CodeAnalysis;

namespace AlephMapper.Models;

internal sealed class AdaptationModel
{
    public AdaptationModel(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        string? generatedName,
        AdaptGeneration generation,
        NullConditionalRewrite nullStrategy,
        AttributeData attribute)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        GeneratedName = generatedName;
        Generation = generation;
        NullStrategy = nullStrategy;
        Attribute = attribute;
    }

    public INamedTypeSymbol SourceType { get; }
    public INamedTypeSymbol DestinationType { get; }
    public string? GeneratedName { get; }
    public AdaptGeneration Generation { get; }
    public NullConditionalRewrite NullStrategy { get; }
    public AttributeData Attribute { get; }
}
