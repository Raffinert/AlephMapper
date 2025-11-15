using AlephMapper.CodeGenerators;
using AlephMapper.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using SymbolDisplayFormat = Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace AlephMapper.Helpers;

internal static class EmitHelpers
{
    public static bool TryBuildUpdateAssignmentsWithInlining(ExpressionSyntax inlinedBody, string destPrefix, List<string> lines, MappingModel mm)
    {
        // Seed type collection with the destination (return) type to reliably resolve
        // object-initializer property types without depending on fragile LHS binding
        var propertyInfoCollector = new PropertyTypeInfoCollector(mm.ReturnType, destPrefix);

        if (mm.CollectionPolicy == CollectionPropertiesPolicy.Skip)
        {
            propertyInfoCollector.Visit(inlinedBody);
        }

        var typeContext = propertyInfoCollector.TypeContext;

        var processor = new UpdatableMethodGenerator(destPrefix, typeContext, mm.ParamName);
        List<string> processedLines;

        var srcName = mm.ParamName;

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

        if (SymbolHelpers.CanBeNull(mm.ParamType))
        {
            lines.Add($"if ({srcName} == null) return dest;");
        }

        lines.AddRange(processedLines);

        lines.Add("return dest;");

        return lines.Count > 0;
    }
}
