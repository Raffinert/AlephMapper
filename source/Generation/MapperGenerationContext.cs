using AlephMapper.Adaptation;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
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
        MappingCatalog mappingsByMethod,
        SourceProductionContext sourceProductionContext)
    {
        MapperType = mapperType;
        MappingsByMethod = mappingsByMethod;
        SourceProductionContext = sourceProductionContext;
        AdaptationMembers = new AdaptationMemberPlanner(mapperType);
    }

    public INamedTypeSymbol MapperType { get; }
    public MappingCatalog MappingsByMethod { get; }
    public SourceProductionContext SourceProductionContext { get; }
    public AdaptationMemberPlanner AdaptationMembers { get; }
    public HashSet<string> UsingDirectives { get; } = new(StringComparer.Ordinal);
    public HashSet<string> GeneratedMemberSignatures { get; } = new(StringComparer.Ordinal);
    public StringBuilder Members { get; } = new();

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
