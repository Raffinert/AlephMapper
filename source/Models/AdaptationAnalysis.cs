#nullable enable

using Microsoft.CodeAnalysis;

namespace AlephMapper.Models;

/// <summary>
/// Compiler-bound adaptation analysis state used only while rendering output.
/// </summary>
internal sealed class AdaptationAnalysis
{
    public AdaptationAnalysis(
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
    private SourceLocationModel(
        string? filePath,
        int start,
        int length,
        int startLine,
        int startCharacter,
        int endLine,
        int endCharacter)
    {
        FilePath = filePath;
        Start = start;
        Length = length;
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
    }

    public string? FilePath { get; }
    public int Start { get; }
    public int Length { get; }
    public int StartLine { get; }
    public int StartCharacter { get; }
    public int EndLine { get; }
    public int EndCharacter { get; }

    public static SourceLocationModel? FromSyntax(SyntaxReference? syntaxReference)
    {
        if (syntaxReference is null)
        {
            return null;
        }

        var syntax = syntaxReference.GetSyntax();
        var location = syntax.GetLocation();
        var lineSpan = location.GetLineSpan();
        return new SourceLocationModel(
            lineSpan.Path,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character,
            lineSpan.EndLinePosition.Line,
            lineSpan.EndLinePosition.Character);
    }

    public Location ToLocation()
    {
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(Start, Length);
        var lineSpan = new Microsoft.CodeAnalysis.Text.LinePositionSpan(
            new Microsoft.CodeAnalysis.Text.LinePosition(StartLine, StartCharacter),
            new Microsoft.CodeAnalysis.Text.LinePosition(EndLine, EndCharacter));
        return Microsoft.CodeAnalysis.Location.Create(FilePath ?? string.Empty, span, lineSpan);
    }
}
