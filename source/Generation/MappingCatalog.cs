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
internal sealed class MappingCatalog
{
    private readonly Dictionary<IMethodSymbol, MappingAnalysis> _mappings;
    private readonly Func<IMethodSymbol, MappingAnalysis?> _createExternalMapping;

    public MappingCatalog(
        Dictionary<IMethodSymbol, MappingAnalysis> mappings,
        Func<IMethodSymbol, MappingAnalysis?> createExternalMapping)
    {
        _mappings = mappings;
        _createExternalMapping = createExternalMapping;
    }

    public bool TryGetValue(IMethodSymbol method, out MappingAnalysis mapping)
    {
        var normalizedMethod = SymbolHelpers.Normalize(method);
        if (_mappings.TryGetValue(normalizedMethod, out mapping))
        {
            return true;
        }

        var externalMapping = _createExternalMapping(normalizedMethod);
        if (externalMapping is null)
        {
            mapping = null!;
            return false;
        }

        _mappings[normalizedMethod] = externalMapping;
        mapping = externalMapping;
        return true;
    }
}
