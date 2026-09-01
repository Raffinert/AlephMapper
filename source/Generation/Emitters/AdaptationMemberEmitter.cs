#nullable enable

using AlephMapper.Adaptation;
using AlephMapper.Diagnostics;
using AlephMapper.Helpers;
using AlephMapper.Models;
using AlephMapper.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Generation.Emitters;

internal static class AdaptationMemberEmitter
{
    public static void Emit(MappingMethodDetails details, MapperGenerationContext context)
    {
        var mapping = details.Mapping;
        if (mapping.MethodSymbol.TypeParameters.Length != 0)
        {
            foreach (var adaptation in mapping.Adaptations)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptGenericMethodUnsupported,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name));
            }

            return;
        }

        var adaptationPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var adaptation in mapping.Adaptations)
        {
            var generateMap = (adaptation.Generation & AdaptGeneration.Map) == AdaptGeneration.Map;
            var generateExpression = (adaptation.Generation & AdaptGeneration.Expression) == AdaptGeneration.Expression;
            var generateUpdate = (adaptation.Generation & AdaptGeneration.Update) == AdaptGeneration.Update;
            if (generateExpression && string.IsNullOrWhiteSpace(adaptation.GeneratedName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptExpressionWithoutName,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name));
                continue;
            }

            var adaptationName = string.IsNullOrWhiteSpace(adaptation.GeneratedName) ? mapping.Name : adaptation.GeneratedName!;
            var sourceTypeName = TypeDisplay.ForSymbol(adaptation.SourceType, NullableAnnotation.None, details.NullableContext);
            var destinationTypeName = TypeDisplay.ForSymbol(adaptation.DestinationType, NullableAnnotation.None, details.NullableContext);
            var additionalParametersWithNames = string.Join(", ", mapping.Parameters.Skip(1).Select(parameter =>
                $"{TypeDisplay.ForSymbol(parameter.Type, parameter.NullableAnnotation, details.NullableContext)} {parameter.Name}"));
            var adaptationParametersWithNames = sourceTypeName + " " + details.SourceName +
                (string.IsNullOrEmpty(additionalParametersWithNames) ? "" : ", " + additionalParametersWithNames);

            var pairSignature = MethodSignature.Build("", [sourceTypeName, destinationTypeName]);
            if (!adaptationPairs.Add(pairSignature))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptDuplicatePair,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name,
                    adaptation.SourceType.ToDisplayString(),
                    adaptation.DestinationType.ToDisplayString()));
                continue;
            }

            var inliner = new InliningResolver(mapping.SemanticModel, context.MappingsByMethod, false, adaptation.NullStrategy);
            var inlinedBody = ((ExpressionSyntax)inliner.Visit(mapping.BodySyntax.Expression)!)
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia();
            context.AddUsings(inliner.UsingDirectives.Concat(mapping.UsingDirectives));
            if (inliner.CircularReferences.Any())
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptCircularHelper,
                    mapping.MethodSymbol.Locations.FirstOrDefault(),
                    mapping.MethodSymbol.Name));
                continue;
            }

            if ((generateMap || generateExpression) &&
                inliner.UnsafeConditionalReceivers.FirstOrDefault() is { } unsafeReceiver)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnsafeNullConditionalReceiver,
                    unsafeReceiver.Location,
                    mapping.MethodSymbol.Name,
                    unsafeReceiver.Expression.ToString()));
                continue;
            }

            if (generateExpression &&
                inliner.UnsupportedNullConditionals.FirstOrDefault() is { } unsupportedConditional)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedNullConditionalExpression,
                    unsupportedConditional.Location,
                    mapping.MethodSymbol.Name,
                    unsupportedConditional.Expression.ToString()));
                continue;
            }

            if (!AdaptationValidator.Validate(context, mapping, adaptation, inlinedBody))
            {
                continue;
            }

            var adaptedBody = AdaptedDestinationRewriter.Rewrite(
                mapping.BodySyntax.Expression,
                inlinedBody,
                mapping.SemanticModel,
                mapping.ReturnType,
                destinationTypeName,
                adaptation.DestinationType,
                details.NullableContext);
            var adaptedBodyText = PrettyPrinter.Print(adaptedBody, 2);

            var adaptationParameterTypes = new[] { sourceTypeName }.Concat(details.ParameterTypeNames.Skip(1)).ToArray();
            var mapSignature = MethodSignature.Build(adaptationName, adaptationParameterTypes);
            var updateParameterTypes = adaptationParameterTypes.Append(destinationTypeName).ToArray();
            var updateSignature = MethodSignature.Build(adaptationName, updateParameterTypes);
            var expressionName = adaptationName + "Expression";
            var expressionSignature = MethodSignature.Build(expressionName, details.ExtraExpressionParameterTypeNames);
            var generatedConflict = (generateMap && context.GeneratedMemberSignatures.Contains(mapSignature)) ||
                                    (generateExpression && context.GeneratedMemberSignatures.Contains(expressionSignature)) ||
                                    (generateUpdate && context.GeneratedMemberSignatures.Contains(updateSignature));
            var plannerConflict = !context.AdaptationMembers.TryReserve(
                adaptationName,
                adaptationParameterTypes,
                details.ExtraExpressionParameterTypeNames,
                generateMap,
                generateExpression,
                generateUpdate,
                updateParameterTypes,
                out var conflict);
            if (generatedConflict || plannerConflict)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptNameConflict,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name,
                    generatedConflict
                        ? (generateMap && context.GeneratedMemberSignatures.Contains(mapSignature)
                            ? mapSignature
                            : generateExpression && context.GeneratedMemberSignatures.Contains(expressionSignature)
                                ? expressionSignature
                                : updateSignature)
                        : conflict));
                continue;
            }

            if (generateMap)
            {
                context.GeneratedMemberSignatures.Add(mapSignature);
                EmitMapMember(details, adaptationName, destinationTypeName, adaptationParametersWithNames, adaptedBodyText, context);
            }

            if (generateExpression)
            {
                context.GeneratedMemberSignatures.Add(expressionSignature);
                var functionArguments = string.Join(", ", new[] { sourceTypeName, destinationTypeName });
                EmitExpressionMember(details, expressionName, functionArguments, adaptedBodyText, context);
            }

            if (generateUpdate)
            {
                if (adaptation.DestinationType.IsValueType && !SymbolHelpers.CanBeNull(adaptation.DestinationType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UpdatableValueTypeReturn,
                        GetLocation(mapping, adaptation),
                        adaptationName,
                        adaptation.DestinationType.ToDisplayString()));
                    continue;
                }

                var updateInliner = new InliningResolver(mapping.SemanticModel, context.MappingsByMethod, true, NullConditionalRewrite.None);
                var inlinedUpdateBody = (ExpressionSyntax)updateInliner.Visit(mapping.BodySyntax.Expression)!.WithoutTrivia();
                context.AddUsings(updateInliner.UsingDirectives.Concat(mapping.UsingDirectives));
                if (updateInliner.CircularReferences.Any())
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UpdatableCircularReferences,
                        GetLocation(mapping, adaptation),
                        adaptationName));
                    continue;
                }

                var adaptedUpdateBody = AdaptedDestinationRewriter.Rewrite(
                    mapping.BodySyntax.Expression,
                    inlinedUpdateBody,
                    mapping.SemanticModel,
                    mapping.ReturnType,
                    destinationTypeName,
                    adaptation.DestinationType,
                    details.NullableContext);
                var lines = new List<string>();
                if (!EmitHelpers.TryBuildUpdateAssignmentsWithInlining(
                        adaptedUpdateBody,
                        "dest",
                        adaptation.DestinationType,
                        adaptation.SourceType,
                        mapping.Parameters.Select(parameter => parameter.Name).ToArray(),
                        mapping.CollectionPolicy,
                        lines))
                {
                    continue;
                }

                context.GeneratedMemberSignatures.Add(updateSignature);
                EmitUpdateMember(
                    details,
                    adaptationName,
                    adaptationParametersWithNames,
                    destinationTypeName,
                    lines,
                    context);
            }
        }
    }

    private static void EmitMapMember(
        MappingMethodDetails details,
        string adaptationName,
        string destinationTypeName,
        string parametersWithNames,
        string adaptedBodyText,
        MapperGenerationContext context)
    {
        context.AppendMember(members =>
        {
            members.AppendLine("    /// <summary>");
            members.AppendLine($"    /// This is an auto-generated adapted mapping method for <see cref=\"{details.Mapping.Name}({details.MethodParameterList})\"/>.");
            members.AppendLine("    /// </summary>");
            members.AppendLine($"    public static {destinationTypeName} {adaptationName}({parametersWithNames}) =>");
            members.AppendLine("        " + adaptedBodyText + ";");
        });
    }

    private static void EmitExpressionMember(
        MappingMethodDetails details,
        string expressionName,
        string functionArguments,
        string adaptedBodyText,
        MapperGenerationContext context)
    {
        context.AppendMember(members =>
        {
            members.AppendLine("    /// <summary>");
            members.AppendLine($"    /// This is an auto-generated adapted expression companion for <see cref=\"{details.Mapping.Name}({details.MethodParameterList})\"/>.");
            members.AppendLine("    /// </summary>");
            var expressionMethodParameters = string.IsNullOrEmpty(details.ExtraExpressionParameterListWithNames)
                ? "()"
                : "(" + details.ExtraExpressionParameterListWithNames + ")";
            members.AppendLine("    public static global::System.Linq.Expressions.Expression<global::System.Func<" + functionArguments + ">> " + expressionName + expressionMethodParameters + " =>");
            members.Append("        " + details.ProjectionLambdaParameter + " => ");
            members.AppendLine(adaptedBodyText + ";");
        });
    }

    private static void EmitUpdateMember(
        MappingMethodDetails details,
        string adaptationName,
        string parametersWithNames,
        string destinationTypeName,
        IEnumerable<string> lines,
        MapperGenerationContext context)
    {
        context.AppendMember(members =>
        {
            members.AppendLine("    /// <summary>");
            members.AppendLine($"    /// This is an auto-generated adapted update method for <see cref=\"{details.Mapping.Name}({details.MethodParameterList})\"/>.");
            members.AppendLine("    /// </summary>");
            members.AppendLine($"    public static {destinationTypeName} {adaptationName}({parametersWithNames}, {destinationTypeName} dest)");
            members.AppendLine("    {");
            foreach (var line in lines)
            {
                members.AppendLine("        " + line);
            }
            members.AppendLine("    }");
        });
    }

    private static Location? GetLocation(MappingAnalysis mapping, AdaptationAnalysis adaptation)
    {
        return adaptation.Location?.ToLocation() ??
               mapping.MethodSymbol.Locations.FirstOrDefault();
    }
}
