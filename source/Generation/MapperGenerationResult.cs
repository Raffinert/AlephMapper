#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using AlephMapper.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace AlephMapper.Generation;

/// <summary>
/// Value-only output from mapper analysis. This is the boundary between
/// compiler-bound semantic rewriting and incremental source production.
/// </summary>
internal sealed class MapperGenerationResult(
    string? hintName,
    string? source,
    ImmutableArray<GenerationDiagnostic> diagnostics)
    : IEquatable<MapperGenerationResult>
{
    public string? HintName { get; } = hintName;
    public string? Source { get; } = source;
    public ImmutableArray<GenerationDiagnostic> Diagnostics { get; } = diagnostics;

    public static MapperGenerationResult Empty { get; } = new MapperGenerationResult(null, null, ImmutableArray<GenerationDiagnostic>.Empty);

    public bool Equals(MapperGenerationResult? other)
    {
        return other is not null &&
               string.Equals(HintName, other.HintName, StringComparison.Ordinal) &&
               string.Equals(Source, other.Source, StringComparison.Ordinal) &&
               Diagnostics.SequenceEqual(other.Diagnostics);
    }

    public override bool Equals(object? obj) => Equals(obj as MapperGenerationResult);
    public override int GetHashCode() => (HintName, Source).GetHashCode();
}

internal readonly struct GenerationDiagnostic(
    string id,
    string title,
    string message,
    DiagnosticSeverity severity,
    string category,
    string? filePath,
    int start,
    int length,
    int startLine,
    int startCharacter,
    int endLine,
    int endCharacter)
    : IEquatable<GenerationDiagnostic>
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public string Message { get; } = message;
    public DiagnosticSeverity Severity { get; } = severity;
    public string Category { get; } = category;
    public string? FilePath { get; } = filePath;
    public int Start { get; } = start;
    public int Length { get; } = length;
    public int StartLine { get; } = startLine;
    public int StartCharacter { get; } = startCharacter;
    public int EndLine { get; } = endLine;
    public int EndCharacter { get; } = endCharacter;

    public static GenerationDiagnostic From(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var lineSpan = location == Location.None ? default : location.GetLineSpan();
        return new GenerationDiagnostic(
            diagnostic.Id,
            diagnostic.Descriptor.Title.ToString(),
            diagnostic.GetMessage(),
            diagnostic.Severity,
            diagnostic.Descriptor.Category,
            location == Location.None ? null : location.SourceTree?.FilePath ?? lineSpan.Path,
            location == Location.None ? 0 : location.SourceSpan.Start,
            location == Location.None ? 0 : location.SourceSpan.Length,
            location == Location.None ? 0 : lineSpan.StartLinePosition.Line,
            location == Location.None ? 0 : lineSpan.StartLinePosition.Character,
            location == Location.None ? 0 : lineSpan.EndLinePosition.Line,
            location == Location.None ? 0 : lineSpan.EndLinePosition.Character);
    }

    public Diagnostic ToDiagnostic(Compilation compilation)
    {
        var descriptor = CreateDescriptor();
        if (string.IsNullOrEmpty(FilePath))
        {
            return Diagnostic.Create(descriptor, Location.None);
        }

        var span = new TextSpan(Start, Length);
        var filePath = FilePath;
        var sourceTree = compilation.SyntaxTrees.FirstOrDefault(tree =>
            string.Equals(tree.FilePath, filePath, StringComparison.Ordinal));
        if (sourceTree is not null && span.End <= sourceTree.GetText().Length)
        {
            return Diagnostic.Create(descriptor, Location.Create(sourceTree, span));
        }

        var lineSpan = new LinePositionSpan(
            new LinePosition(StartLine, StartCharacter),
            new LinePosition(EndLine, EndCharacter));
        return Diagnostic.Create(descriptor, Location.Create(FilePath!, span, lineSpan));
    }

    private DiagnosticDescriptor CreateDescriptor()
    {
        var original = DiagnosticDescriptors.GetById(Id);
        return original is null
            ? new DiagnosticDescriptor(Id, Title, Message, Category, Severity, isEnabledByDefault: true)
            : new DiagnosticDescriptor(
                original.Id,
                original.Title,
                Message,
                original.Category,
                original.DefaultSeverity,
                original.IsEnabledByDefault,
                original.Description,
                original.HelpLinkUri,
                original.CustomTags.ToArray());
    }

    public bool Equals(GenerationDiagnostic other)
    {
        return Id == other.Id && Title == other.Title && Message == other.Message && Severity == other.Severity &&
               Category == other.Category && FilePath == other.FilePath && Start == other.Start && Length == other.Length &&
               StartLine == other.StartLine && StartCharacter == other.StartCharacter &&
               EndLine == other.EndLine && EndCharacter == other.EndCharacter;
    }

    public override bool Equals(object? obj) => obj is GenerationDiagnostic other && Equals(other);
    public override int GetHashCode() => (Id, Message, Start, Length).GetHashCode();
}
