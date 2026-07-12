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
        var adaptationPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var adaptation in mapping.Adaptations)
        {
            var generateMap = (adaptation.Generation & AdaptGeneration.Map) == AdaptGeneration.Map;
            var generateExpression = (adaptation.Generation & AdaptGeneration.Expression) == AdaptGeneration.Expression;
            if (generateExpression && string.IsNullOrWhiteSpace(adaptation.GeneratedName))
            {
                context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
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
                context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptDuplicatePair,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name,
                    adaptation.SourceType.ToDisplayString(),
                    adaptation.DestinationType.ToDisplayString()));
                continue;
            }

            var inliner = new InliningResolver(mapping.SemanticModel, context.MappingsByMethod, false, adaptation.NullStrategy);
            var inlinedBody = (ExpressionSyntax)inliner.Visit(mapping.BodySyntax.Expression)!.WithoutTrivia();
            context.AddUsings(inliner.UsingDirectives.Concat(mapping.UsingDirectives));
            if (inliner.CircularReferences.Any())
            {
                context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptCircularHelper,
                    mapping.MethodSymbol.Locations.FirstOrDefault(),
                    mapping.MethodSymbol.Name));
                continue;
            }

            if (!AdaptationValidator.Validate(context.SourceProductionContext, mapping, adaptation))
            {
                continue;
            }

            var adaptedBody = AdaptedDestinationRewriter.Rewrite(
                mapping.BodySyntax.Expression,
                inlinedBody,
                mapping.SemanticModel,
                mapping.ReturnType,
                destinationTypeName);
            var adaptedBodyText = PrettyPrinter.Print(adaptedBody, 2);

            var adaptationParameterTypes = new[] { sourceTypeName }.Concat(details.ParameterTypeNames.Skip(1)).ToArray();
            var mapSignature = MethodSignature.Build(adaptationName, adaptationParameterTypes);
            var expressionName = adaptationName + "Expression";
            var expressionSignature = expressionName + "()";
            var generatedConflict = (generateMap && context.GeneratedMemberSignatures.Contains(mapSignature)) ||
                                    (generateExpression && context.GeneratedMemberSignatures.Contains(expressionSignature));
            var plannerConflict = !context.AdaptationMembers.TryReserve(
                adaptationName,
                adaptationParameterTypes,
                generateMap,
                generateExpression,
                out var conflict);
            if (generatedConflict || plannerConflict)
            {
                context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptNameConflict,
                    GetLocation(mapping, adaptation),
                    mapping.MethodSymbol.Name,
                    generatedConflict
                        ? (generateMap && context.GeneratedMemberSignatures.Contains(mapSignature) ? mapSignature : expressionSignature)
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
                var functionArguments = string.Join(", ", new[] { sourceTypeName }.Concat(details.ParameterTypeNames.Skip(1)).Append(destinationTypeName));
                EmitExpressionMember(details, expressionName, functionArguments, adaptedBodyText, context);
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
            members.AppendLine("    public static Expression<Func<" + functionArguments + ">> " + expressionName + "() => ");
            members.Append("        " + details.LambdaParameters + " => ");
            members.AppendLine(adaptedBodyText + ";");
        });
    }

    private static Location? GetLocation(MappingModel mapping, AdaptationModel adaptation)
    {
        return adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
               mapping.MethodSymbol.Locations.FirstOrDefault();
    }
}
