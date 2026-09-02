using AlephMapper.Adaptation;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace AlephMapper.Generation;

/// <summary>
/// Holds the mutable state for one generated mapper file. Feature emitters use
/// this instead of sharing source-output orchestration concerns.
/// </summary>
internal sealed class MapperGenerationContext(
    INamedTypeSymbol mapperType,
    MappingCatalog mappingsByMethod)
{
    private bool _hasMembers;

    public INamedTypeSymbol MapperType { get; } = mapperType;
    public MappingCatalog MappingsByMethod { get; } = mappingsByMethod;
    public AdaptationMemberPlanner AdaptationMembers { get; } = new(mapperType);
    public HashSet<string> UsingDirectives { get; } = new(StringComparer.Ordinal);
    public HashSet<string> GeneratedMemberSignatures { get; } = new(StringComparer.Ordinal);
    public StringBuilder Members { get; } = new();
    private List<GenerationDiagnostic> Diagnostics { get; } = new();

    public ImmutableArray<GenerationDiagnostic> GetDiagnostics() => [.. Diagnostics];

    public void ReportDiagnostic(Diagnostic diagnostic) => Diagnostics.Add(GenerationDiagnostic.From(diagnostic));

    public void AddUsings(IEnumerable<string> usingDirectives)
    {
        UsingDirectives.UnionWith(usingDirectives);
    }

    public void AppendMember(Action<StringBuilder> writeMember)
    {
        if (_hasMembers)
        {
            Members.AppendLine();
        }

        writeMember(Members);

        _hasMembers = true;
    }
}
