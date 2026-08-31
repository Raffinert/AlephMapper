#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Threading;

namespace AlephMapper.Generation;

/// <summary>
/// Value-only identity for a mapper declaration. One instance drives one
/// generated mapper file and therefore forms the incremental output unit.
/// </summary>
internal sealed class MapperCandidate : IEquatable<MapperCandidate>
{
    private MapperCandidate(string filePath, int start, int length)
    {
        FilePath = filePath;
        Start = start;
        Length = length;
    }

    public string FilePath { get; }
    public int Start { get; }
    public int Length { get; }

    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        if (node is not ClassDeclarationSyntax containingClass ||
            !containingClass.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        return containingClass.AttributeLists.Count != 0 ||
               containingClass.Members.OfType<MethodDeclarationSyntax>().Any(static method =>
                   method.AttributeLists.Count != 0);
    }

    public static MapperCandidate Create(SyntaxNode node, CancellationToken _)
    {
        return new MapperCandidate(node.SyntaxTree.FilePath, node.SpanStart, node.Span.Length);
    }

    public bool Equals(MapperCandidate? other)
    {
        return other is not null &&
               Start == other.Start &&
               Length == other.Length &&
               string.Equals(FilePath, other.FilePath, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as MapperCandidate);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 397) ^ Start;
            return (hash * 397) ^ Length;
        }
    }
}
