#nullable enable

using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace AlephMapper.Generation;

/// <summary>
/// Resolves local mappings eagerly and source-defined helper mappings lazily.
/// This keeps each mapper output independent while preserving cross-mapper
/// inlining support.
/// </summary>
internal sealed class MappingCatalog(
    Dictionary<IMethodSymbol, MappingAnalysis> mappings,
    Func<IMethodSymbol, MappingAnalysis?> createExternalMapping)
{
    private readonly Dictionary<IPropertySymbol, MappingAnalysis> properties = new(SymbolEqualityComparer.Default);

    public bool TryGetProperty(IPropertySymbol property, out MappingAnalysis mapping)
    {
        property = property.OriginalDefinition;
        if (properties.TryGetValue(property, out mapping)) return true;
        if (property.GetMethod is not null && TryGetValue(property.GetMethod, out mapping))
        {
            properties[property] = mapping;
            return true;
        }
        mapping = null!;
        return false;
    }

    public bool TryGetValue(IMethodSymbol method, out MappingAnalysis mapping)
    {
        var normalizedMethod = SymbolHelpers.Normalize(method);
        if (mappings.TryGetValue(normalizedMethod, out mapping))
        {
            return true;
        }

        var externalMapping = createExternalMapping(normalizedMethod);
        if (externalMapping is null)
        {
            mapping = null!;
            return false;
        }

        mappings[normalizedMethod] = externalMapping;
        mapping = externalMapping;
        return true;
    }
}
