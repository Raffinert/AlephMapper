using AlephMapper.Adaptation;
using AlephMapper.Helpers;
using AlephMapper.Models;
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
internal sealed class MapperGenerationContext
{
    private bool _hasMembers;

    public MapperGenerationContext(
        INamedTypeSymbol mapperType,
        MappingCatalog mappingsByMethod)
    {
        MapperType = mapperType;
        MappingsByMethod = mappingsByMethod;
        AdaptationMembers = new AdaptationMemberPlanner(mapperType);
    }

    public INamedTypeSymbol MapperType { get; }
    public MappingCatalog MappingsByMethod { get; }
    public AdaptationMemberPlanner AdaptationMembers { get; }
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

    public void AppendMember(NullablePolicy nullablePolicy, Action<StringBuilder> writeMember)
    {
        if (_hasMembers)
        {
            Members.AppendLine();
        }

        Members.AppendLine($"#nullable {nullablePolicy.Directive}");

        writeMember(Members);
        Members.AppendLine("#nullable restore");

        _hasMembers = true;
    }
}
