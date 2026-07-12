#nullable enable

using AlephMapper.Helpers;
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
                .Select(m => BuildMethodSignature(
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
        bool generateMap,
        bool generateExpression,
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

            requestedSignatures.Add(BuildMethodSignature(mapName, mapParameterTypes));
        }

        if (generateExpression)
        {
            var expressionName = mapName + "Expression";
            if (_existingNonMethodNames.Contains(expressionName) || _generatedNonMethodNames.Contains(expressionName))
            {
                conflict = expressionName;
                return false;
            }

            requestedSignatures.Add(BuildMethodSignature(expressionName, []));
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

    private static string BuildMethodSignature(string name, IEnumerable<string> parameterTypeNames)
    {
        return name + "(" + string.Join(",", parameterTypeNames.Select(RemoveNullableSignatureMarker)) + ")";
    }

    private static string RemoveNullableSignatureMarker(string typeName)
    {
        return typeName.EndsWith("?", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 1) : typeName;
    }
}
