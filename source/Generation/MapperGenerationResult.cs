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
internal sealed class MapperGenerationResult : IEquatable<MapperGenerationResult>
{
    public MapperGenerationResult(string? hintName, string? source, ImmutableArray<GenerationDiagnostic> diagnostics)
    {
        HintName = hintName;
        Source = source;
        Diagnostics = diagnostics;
    }

    public string? HintName { get; }
    public string? Source { get; }
    public ImmutableArray<GenerationDiagnostic> Diagnostics { get; }

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

internal readonly struct GenerationDiagnostic : IEquatable<GenerationDiagnostic>
{
    public GenerationDiagnostic(
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
    {
        Id = id;
        Title = title;
        Message = message;
        Severity = severity;
        Category = category;
        FilePath = filePath;
        Start = start;
        Length = length;
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
    }

    public string Id { get; }
    public string Title { get; }
    public string Message { get; }
    public DiagnosticSeverity Severity { get; }
    public string Category { get; }
    public string? FilePath { get; }
    public int Start { get; }
    public int Length { get; }
    public int StartLine { get; }
    public int StartCharacter { get; }
    public int EndLine { get; }
    public int EndCharacter { get; }
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

    public Diagnostic ToDiagnostic()
    {
        var descriptor = DiagnosticDescriptors.GetById(Id) ??
            new DiagnosticDescriptor(Id, Title, Message, Category, Severity, isEnabledByDefault: true);
        if (string.IsNullOrEmpty(FilePath))
        {
            return Diagnostic.Create(descriptor, Location.None);
        }

        var span = new TextSpan(Start, Length);
        var lineSpan = new LinePositionSpan(
            new LinePosition(StartLine, StartCharacter),
            new LinePosition(EndLine, EndCharacter));
        return Diagnostic.Create(descriptor, Location.Create(FilePath!, span, lineSpan));
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
