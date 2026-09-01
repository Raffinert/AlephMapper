#nullable enable

using System;

namespace AlephMapper.Generation;

/// <summary>
/// The source-only portion of a mapper generation result. Keeping this value
/// separate ensures diagnostic changes do not invalidate source emission.
/// </summary>
internal readonly struct MapperSourceResult(string? hintName, string? source) : IEquatable<MapperSourceResult>
{
    public string? HintName { get; } = hintName;
    public string? Source { get; } = source;

    public bool Equals(MapperSourceResult other) =>
        string.Equals(HintName, other.HintName, StringComparison.Ordinal) &&
        string.Equals(Source, other.Source, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is MapperSourceResult other && Equals(other);
    public override int GetHashCode() => (HintName, Source).GetHashCode();
}
