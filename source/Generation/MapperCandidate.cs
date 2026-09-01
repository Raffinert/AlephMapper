#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Threading;

namespace AlephMapper.Generation;

/// <summary>
/// Value-only identity for an AlephMapper attribute target. The target is
/// kept separate from its containing mapper so multiple configuration kinds
/// can be normalized to one generated file without caching a symbol.
/// </summary>
internal sealed class MapperCandidate : IEquatable<MapperCandidate>
{
    internal MapperCandidate(
        string filePath,
        int start,
        int length,
        MapperAttributeKind attributeKind)
    {
        FilePath = filePath;
        Start = start;
        Length = length;
        AttributeKind = attributeKind;
    }

    public string FilePath { get; }
    public int Start { get; }
    public int Length { get; }
    public MapperAttributeKind AttributeKind { get; }

    public static bool IsAttributeTarget(SyntaxNode node, CancellationToken _)
    {
        var containingClass = node switch
        {
            ClassDeclarationSyntax classDeclaration => classDeclaration,
            MethodDeclarationSyntax { Parent: ClassDeclarationSyntax classDeclaration } => classDeclaration,
            _ => null
        };

        if (containingClass is null)
        {
            return false;
        }

        foreach (var modifier in containingClass.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.StaticKeyword))
            {
                return true;
            }
        }

        return false;
    }

    public static MapperCandidate Create(
        GeneratorAttributeSyntaxContext context,
        MapperAttributeKind attributeKind,
        CancellationToken _)
    {
        var targetNode = context.TargetNode;
        return new MapperCandidate(
            targetNode.SyntaxTree.FilePath,
            targetNode.SpanStart,
            targetNode.Span.Length,
            attributeKind);
    }

    public bool Equals(MapperCandidate? other)
    {
        return other is not null &&
               Start == other.Start &&
               Length == other.Length &&
               AttributeKind == other.AttributeKind &&
               string.Equals(FilePath, other.FilePath, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as MapperCandidate);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(FilePath);
            hash = (hash * 397) ^ Start;
            hash = (hash * 397) ^ Length;
            return (hash * 397) ^ (int)AttributeKind;
        }
    }
}

internal enum MapperAttributeKind
{
    Projectable,
    Updatable,
    Adapt
}
