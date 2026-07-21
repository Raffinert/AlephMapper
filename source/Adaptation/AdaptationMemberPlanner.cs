#nullable enable

using AlephMapper.Helpers;
using AlephMapper.Generation;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Adaptation;

/// <summary>
/// Coordinates generated-member reservations for one mapper type.
/// Reservations are made only after an adaptation has been validated.
/// </summary>
internal sealed class AdaptationMemberPlanner
{
    private readonly HashSet<string> _existingMethodSignatures;
    private readonly HashSet<string> _existingNonMethodNames;
    private readonly HashSet<string> _generatedMethodSignatures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _generatedNonMethodNames = new(StringComparer.Ordinal);

    public AdaptationMemberPlanner(INamedTypeSymbol mapperType)
    {
        _existingMethodSignatures = new HashSet<string>(
            mapperType.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
                .Select(m => MethodSignature.Build(
                    m.Name,
                    m.Parameters.Select(p => TypeDisplay.ForSymbol(p.Type, p.NullableAnnotation, NullableContext.Disabled)))),
            StringComparer.Ordinal);

        _existingNonMethodNames = new HashSet<string>(
            mapperType.GetMembers()
                .Where(m => m is not IMethodSymbol)
                .Select(m => m.Name),
            StringComparer.Ordinal);
    }

    public bool TryReserve(
        string mapName,
        IEnumerable<string> mapParameterTypes,
        IEnumerable<string> expressionParameterTypes,
        bool generateMap,
        bool generateExpression,
        bool generateUpdate,
        IEnumerable<string> updateParameterTypes,
        out string conflict)
    {
        var requestedSignatures = new List<string>();
        if (generateMap)
        {
            if (_existingNonMethodNames.Contains(mapName) || _generatedNonMethodNames.Contains(mapName))
            {
                conflict = mapName;
                return false;
            }

            requestedSignatures.Add(MethodSignature.Build(mapName, mapParameterTypes));
        }

        if (generateExpression)
        {
            var expressionName = mapName + "Expression";
            if (_existingNonMethodNames.Contains(expressionName) || _generatedNonMethodNames.Contains(expressionName))
            {
                conflict = expressionName;
                return false;
            }

            requestedSignatures.Add(MethodSignature.Build(expressionName, expressionParameterTypes));
        }

        if (generateUpdate)
        {
            if (_existingNonMethodNames.Contains(mapName) || _generatedNonMethodNames.Contains(mapName))
            {
                conflict = mapName;
                return false;
            }

            requestedSignatures.Add(MethodSignature.Build(mapName, updateParameterTypes));
        }

        foreach (var signature in requestedSignatures)
        {
            if (_existingMethodSignatures.Contains(signature) || _generatedMethodSignatures.Contains(signature))
            {
                conflict = signature;
                return false;
            }
        }

        _generatedMethodSignatures.UnionWith(requestedSignatures);
        conflict = string.Empty;
        return true;
    }
}
