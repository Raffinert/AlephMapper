using AlephMapper.Diagnostics;
using AlephMapper.Helpers;
using AlephMapper.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Generation.Emitters;

internal static class UpdatableMemberEmitter
{
    public static void Emit(MappingMethodDetails details, MapperGenerationContext context)
    {
        var mapping = details.Mapping;
        if (!mapping.IsUpdatable)
        {
            return;
        }

        var inliner = new InliningResolver(mapping.SemanticModel, context.MappingsByMethod, true, NullConditionalRewrite.None);
        var inlinedBody = inliner.Visit(mapping.BodySyntax.Expression)!.WithoutTrivia();
        context.AddUsings(inliner.UsingDirectives.Concat(mapping.UsingDirectives));
        if (inliner.CircularReferences.Any())
        {
            context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UpdatableCircularReferences,
                mapping.MethodSymbol.Locations.FirstOrDefault(),
                mapping.MethodSymbol.Name));
            return;
        }

        if (mapping.ReturnType.IsValueType && !SymbolHelpers.CanBeNull(mapping.ReturnType))
        {
            context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UpdatableValueTypeReturn,
                mapping.MethodSymbol.Locations.FirstOrDefault(),
                mapping.MethodSymbol.Name,
                mapping.ReturnType.ToDisplayString()));
            return;
        }

        var lines = new List<string>();
        var replacedMethod = mapping.BodySyntax.ReplaceNode(mapping.BodySyntax.Expression, inlinedBody);
        if (!EmitHelpers.TryBuildUpdateAssignmentsWithInlining(replacedMethod.Expression, "dest", lines, mapping))
        {
            return;
        }

        context.AppendMember(members =>
        {
            members.AppendLine("    /// <summary>");
            members.AppendLine($"    /// This is an auto-generated update method for <see cref=\"{mapping.Name}({details.MethodParameterList})\"/>.");
            members.AppendLine("    /// </summary>");
            members.AppendLine($"    /// <param name=\"{details.SourceName}\">The source object to map values from. If null, no updates are performed.</param>");
            foreach (var parameter in mapping.Parameters.Skip(1))
            {
                members.AppendLine($"    /// <param name=\"{parameter.Name}\"/>");
            }
            members.AppendLine("    /// <param name=\"dest\">The destination object to update. If null, the new instance is created.</param>");
            members.AppendLine("    /// <returns>The updated destination object for method chaining, or the new destination instance if either parameter is null.</returns>");
            members.AppendLine("    public static " + details.DestinationTypeName + " " + mapping.Name + "(" + details.MethodParameterListWithNames + ", " + details.DestinationTypeName + " dest)");
            members.AppendLine("    {");
            foreach (var line in lines)
            {
                members.AppendLine("        " + line);
            }
            members.AppendLine("    }");
        });
    }
}
