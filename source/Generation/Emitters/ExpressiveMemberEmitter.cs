using AlephMapper.Diagnostics;
using AlephMapper.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace AlephMapper.Generation.Emitters;

internal static class ExpressiveMemberEmitter
{
    public static void Emit(MappingMethodDetails details, MapperGenerationContext context)
    {
        var mapping = details.Mapping;
        if (!mapping.IsExpressive)
        {
            return;
        }

        var inliner = new InliningResolver(mapping.SemanticModel, context.MappingsByMethod, false, mapping.NullStrategy);
        var inlinedBody = inliner.Visit(mapping.BodySyntax.Expression)!.WithoutTrivia();
        context.AddUsings(inliner.UsingDirectives.Concat(mapping.UsingDirectives));

        if (inliner.CircularReferences.Any())
        {
            context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ExpressiveCircularReferences,
                mapping.MethodSymbol.Locations.FirstOrDefault(),
                mapping.MethodSymbol.Name));
            return;
        }

        if (inliner.UnsafeConditionalReceivers.FirstOrDefault() is { } unsafeReceiver)
        {
            context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsafeNullConditionalReceiver,
                unsafeReceiver.Location,
                mapping.MethodSymbol.Name,
                unsafeReceiver.Expression.ToString()));
            return;
        }

        if (inliner.UnsupportedNullConditionals.FirstOrDefault() is { } unsupportedConditional)
        {
            context.SourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedNullConditionalExpression,
                unsupportedConditional.Location,
                mapping.MethodSymbol.Name,
                unsupportedConditional.Expression.ToString()));
            return;
        }

        var expressionMethodName = mapping.Name + "Expression";
        context.GeneratedMemberSignatures.Add(MethodSignature.Build(expressionMethodName, details.ExtraExpressionParameterTypeNames));
        var nullStrategyDescription = mapping.NullStrategy switch
        {
            NullConditionalRewrite.None => "Null-conditional operators are preserved as-is in the expression tree.",
            NullConditionalRewrite.Ignore => "Null-conditional operators are ignored and treated as regular member access.",
            NullConditionalRewrite.Rewrite => "Null-conditional operators are rewritten as explicit null checks for better compatibility.",
            _ => "Default null handling strategy is applied."
        };
        var funcTypeArguments = string.Join(", ", new[] { details.SourceTypeName, details.DestinationTypeName });
        var prettyBody = PrettyPrinter.Print(inlinedBody, 2);
        var expressionMethodParameters = string.IsNullOrEmpty(details.ExtraExpressionParameterListWithNames)
            ? "()"
            : "(" + details.ExtraExpressionParameterListWithNames + ")";

        context.AppendMember(members =>
        {
            members.AppendLine("    /// <summary>");
            members.AppendLine($"    /// This is an auto-generated expression companion for <see cref=\"{mapping.Name}({details.MethodParameterList})\"/>.");
            members.AppendLine("    /// </summary>");
            members.AppendLine("    /// <remarks>");
            members.AppendLine("    /// <para>");
            members.AppendLine($"    /// Null handling strategy: {nullStrategyDescription}");
            members.AppendLine("    /// </para>");
            members.AppendLine("    /// </remarks>");
            members.AppendLine("    public static Expression<Func<" + funcTypeArguments + ">> " + expressionMethodName + expressionMethodParameters + " => ");
            members.Append("        " + details.ProjectionLambdaParameter + " => ");
            members.AppendLine(prettyBody + ";");
        });
    }
}
