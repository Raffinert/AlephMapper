#nullable enable

using AlephMapper.CodeGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AlephMapper.Helpers;

internal static class EmitHelpers
{
    /// <summary>
    /// Builds the statements of an update companion. Returns <c>false</c> when no update can be generated;
    /// <paramref name="failureReason"/> is set when the mapping shape is recognized but cannot be applied
    /// to an existing destination (and the caller should report it), and is <c>null</c> for shapes that are
    /// simply not update candidates.
    /// </summary>
    public static bool TryBuildUpdateAssignmentsWithInlining(
        ExpressionSyntax inlinedBody,
        string destPrefix,
        ITypeSymbol destinationType,
        ITypeSymbol sourceType,
        IReadOnlyList<string> sourceParameterNames,
        CollectionPropertiesPolicy collectionPolicy,
        NullablePolicy nullablePolicy,
        List<string> lines,
        out string? failureReason)
    {
        var processor = new UpdatableMethodGenerator(sourceParameterNames, collectionPolicy, nullablePolicy);
        if (!processor.TryProcessRoot(inlinedBody, destPrefix, destinationType, out var processedLines))
        {
            failureReason = processor.FailureReason;
            return false;
        }

        failureReason = null;

        if (SymbolHelpers.CanBeNull(sourceType))
        {
            var sourceNullCheck = $"if ({sourceParameterNames[0]} == null) ";
            string? sourceGuard = sourceNullCheck + $"return {destPrefix};";

            // A root `source == null ? null : new T { ... }` lowers to the very same guard, and a root
            // `source == null ? throw ... : new T { ... }` must keep throwing instead of returning early.
            if (processedLines.Count > 0 && processedLines[0].StartsWith(sourceNullCheck, System.StringComparison.Ordinal))
            {
                if (processedLines[0] != sourceGuard)
                {
                    sourceGuard = null;
                }
                else
                {
                    processedLines.RemoveAt(0);
                }
            }

            if (sourceGuard != null)
            {
                lines.Add(sourceGuard);
            }
        }

        lines.AddRange(processedLines);
        lines.Add($"return {destPrefix};");

        return true;
    }
}
