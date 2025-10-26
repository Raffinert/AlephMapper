using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using AlephMapper.CodeGenerators;
using AlephMapper.Models;

namespace AlephMapper.Helpers;

internal static class EmitHelpers
{
    public static bool TryBuildUpdateAssignmentsWithInlining(ExpressionSyntax inlinedBody, string destPrefix, List<string> lines, SemanticModel semanticModel, MappingModel mm)
    {
        var propertyInfoCollector = new PropertyTypeInfoCollector(semanticModel, destPrefix);

        if (mm.CollectionPolicy == CollectionPropertiesPolicy.Skip)
        {
            propertyInfoCollector.Visit(inlinedBody);
        }

        var typeContext = propertyInfoCollector.TypeContext;

        var processor = new UpdatableMethodGenerator(destPrefix, typeContext);
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

        var nullCheckConditions = new List<string>();

        if (SymbolHelpers.CanBeNull(mm.ParamType))
        {
            nullCheckConditions.Add($"{srcName} == null");
        }

        if (SymbolHelpers.CanBeNull(mm.ReturnType))
        {
            nullCheckConditions.Add("dest == null");
        }

        // Generate null check only if there are conditions to check
        if (nullCheckConditions.Count > 0)
        {
            lines.Add("if (" + string.Join(" || ", nullCheckConditions) + ") return dest;");
        }

        lines.AddRange(processedLines);

        lines.Add("return dest;");

        return lines.Count > 0;
    }
}