using AlephMapper.CodeGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace AlephMapper.Helpers;

internal static class EmitHelpers
{
    public static bool TryBuildUpdateAssignmentsWithInlining(
        ExpressionSyntax inlinedBody,
        string destPrefix,
        ITypeSymbol destinationType,
        ITypeSymbol sourceType,
        IReadOnlyList<string> sourceParameterNames,
        CollectionPropertiesPolicy collectionPolicy,
        NullablePolicy nullablePolicy,
        List<string> lines)
    {
        // Seed type collection with the destination (return) type to reliably resolve
        // object-initializer property types without depending on fragile LHS binding
        var propertyInfoCollector = new PropertyTypeInfoCollector(destinationType, destPrefix);

        if (collectionPolicy == CollectionPropertiesPolicy.Skip)
        {
            propertyInfoCollector.Visit(inlinedBody);
        }

        var typeContext = propertyInfoCollector.TypeContext;

        var processor = new UpdatableMethodGenerator(destPrefix, typeContext, sourceParameterNames, nullablePolicy);
        List<string> processedLines;

        var srcName = sourceParameterNames[0];

        // Build null check conditions
        switch (inlinedBody)
        {
            case ObjectCreationExpressionSyntax oce:
                if (oce.Initializer?.Expressions == null || oce.Initializer.Expressions.Count == 0)
                    return false;

                processedLines = processor.ProcessObjectCreation(oce);
                break;

            case ConditionalExpressionSyntax conditional:
                // Handle conditional expressions like: condition ? new Type { ... } : null
                // or: condition ? null : new Type { ... }
                processedLines = processor.ProcessRootConditionalExpression(conditional, destPrefix);
                break;

            default:
                return false;
        }

        if (SymbolHelpers.CanBeNull(sourceType))
        {
            lines.Add($"if ({srcName} == null) return dest;");
        }

        lines.AddRange(processedLines);

        lines.Add("return dest;");

        return lines.Count > 0;
    }
}
