#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AlephMapper.Models;

internal sealed class AdaptationModel
{
    public AdaptationModel(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        string? generatedName,
        AdaptGeneration generation,
        NullConditionalRewrite nullStrategy,
        SourceLocationModel? location)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        GeneratedName = generatedName;
        Generation = generation;
        NullStrategy = nullStrategy;
        Location = location;
    }

    public INamedTypeSymbol SourceType { get; }
    public INamedTypeSymbol DestinationType { get; }
    public string? GeneratedName { get; }
    public AdaptGeneration Generation { get; }
    public NullConditionalRewrite NullStrategy { get; }
    public SourceLocationModel? Location { get; }
}

internal sealed class SourceLocationModel
{
    private SourceLocationModel(string? filePath, int start, int length)
    {
        FilePath = filePath;
        Start = start;
        Length = length;
    }

    public string? FilePath { get; }
    public int Start { get; }
    public int Length { get; }

    public static SourceLocationModel? FromSyntax(SyntaxReference? syntaxReference)
    {
        if (syntaxReference is null)
        {
            return null;
        }

        var syntax = syntaxReference.GetSyntax();
        return new SourceLocationModel(syntax.SyntaxTree.FilePath, syntax.Span.Start, syntax.Span.Length);
    }

    public Location ToLocation()
    {
        var span = new TextSpan(Start, Length);
        var lineSpan = new LinePositionSpan(new LinePosition(0, Start), new LinePosition(0, Start + Length));
        return Microsoft.CodeAnalysis.Location.Create(FilePath ?? string.Empty, span, lineSpan);
    }
}
