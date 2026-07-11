using AlephMapper.Helpers;
using AlephMapper.Models;
using AlephMapper.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AlephMapper.Diagnostics;

namespace AlephMapper;

[Generator]
public class AlephSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("AlephMapper.Attributes.g.cs", SourceText.From(GetExpressiveAttributeSource(), Encoding.UTF8)));

        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is MethodDeclarationSyntax && node.Parent is ClassDeclarationSyntax,
            GetMappingModel
        ).Where(static m => m != null);

        var all = candidates.Collect();

        context.RegisterSourceOutput(all, static (spc, models) =>
        {
            try
            {
                if (models.Length == 0) return;

                GenerateSourceCode(models, spc);
            }
            catch (Exception e)
            {
                CrashDiagnosticsReporter.Report(spc, e);
#if DEBUG
                throw;
#endif
            }
        });
    }

    private static void GenerateSourceCode(ImmutableArray<MappingModel> models, SourceProductionContext spc)
    {
        var modelsByMethod = new Dictionary<IMethodSymbol, MappingModel>(SymbolHelpers.MethodComparer.Instance);
        foreach (var mm in models)
        {
            modelsByMethod[SymbolHelpers.Normalize(mm.MethodSymbol)] = mm;
        }

        var modelsByClass = new Dictionary<INamedTypeSymbol, List<MappingModel>>(SymbolEqualityComparer.Default);
        foreach (var mm in models)
        {
            if (!modelsByClass.TryGetValue(mm.ContainingType, out var list))
            {
                list = [];
                modelsByClass.Add(mm.ContainingType, list);
            }
            list.Add(mm);
        }

        foreach (var kvp in modelsByClass)
        {
            var mapperType = kvp.Key;
            var methods = kvp.Value;

            if (!methods.Any(m => (m.IsExpressive || m.IsUpdatable || m.Adaptations.Count > 0) && m.IsClassPartial))
            {
                continue;
            }

            var membersSb = new StringBuilder();

            var allUsingDirectives = new HashSet<string>();

            bool isFirst = true;
            var generatedMemberSignatures = new HashSet<string>(StringComparer.Ordinal);
            var existingMemberNames = new HashSet<string>(mapperType.GetMembers().Select(m => m.Name), StringComparer.Ordinal);
            var existingMethodSignatures = new HashSet<string>(
                mapperType.GetMembers().OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary)
                    .Select(m => BuildMethodSignature(
                        m.Name,
                        m.Parameters.Select(p => TypeDisplay.ForSymbol(p.Type, p.NullableAnnotation, NullableContext.Disabled)))),
                StringComparer.Ordinal);

            foreach (var mm in methods)
            {
                var nullableContextPosition = mm.MethodSymbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0;
                var parameterFqns = mm.Parameters
                    .Select(p => TypeDisplay.ForSymbol(p.Type, p.NullableAnnotation, mm.SemanticModel.GetNullableContext(nullableContextPosition)))
                    .ToArray();
                var destFqn = TypeDisplay.ForSymbol(mm.ReturnType, mm.MethodSymbol.ReturnNullableAnnotation, mm.SemanticModel.GetNullableContext(nullableContextPosition));
                var srcFqn = TypeDisplay.ForSymbol(mm.ParamType, mm.Parameters[0].NullableAnnotation, mm.SemanticModel.GetNullableContext(nullableContextPosition));
                var srcName = mm.Parameters[0].Name;
                var methodParameterList = string.Join(", ", parameterFqns);
                var methodParameterListWithNames = string.Join(", ",
                    mm.Parameters.Select(p => $"{TypeDisplay.ForSymbol(p.Type, p.NullableAnnotation, mm.SemanticModel.GetNullableContext(nullableContextPosition))} {p.Name}"));
                var lambdaParameters = mm.Parameters.Count == 1
                    ? mm.Parameters[0].Name
                    : "(" + string.Join(", ", mm.Parameters.Select(p => p.Name)) + ")";

                if (!mm.IsExpressive && !mm.IsUpdatable && mm.Adaptations.Count == 0) continue;

                // Expression method
                if (mm.IsExpressive)
                {
                    var expressionInliner = new InliningResolver(mm.SemanticModel, modelsByMethod, false, mm.NullStrategy);
                    var inlinedBody = expressionInliner.Visit(mm.BodySyntax.Expression)!.WithoutTrivia();
                    allUsingDirectives.UnionWith(expressionInliner.UsingDirectives.Concat(mm.UsingDirectives));

                    // Skip generating expression method if there are circular references
                    if (expressionInliner.CircularReferences.Any())
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ExpressiveCircularReferences,
                            mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name);

                        spc.ReportDiagnostic(diagnostic);
                        continue; // Skip expression generation
                    }

                    var expressionMethodName = mm.Name + "Expression";
                    generatedMemberSignatures.Add(expressionMethodName + "()");

                    if (!isFirst)
                    {
                        membersSb.AppendLine();
                    }
                    isFirst = false;
                    membersSb.AppendLine("    /// <summary>");
                    membersSb.AppendLine($"    /// This is an auto-generated expression companion for <see cref=\"{mm.Name}({methodParameterList})\"/>.");
                    membersSb.AppendLine("    /// </summary>");
                    membersSb.AppendLine("    /// <remarks>");

                    // Add null strategy information
                    string nullStrategyDescription = mm.NullStrategy switch
                    {
                        NullConditionalRewrite.None => "Null-conditional operators are preserved as-is in the expression tree.",
                        NullConditionalRewrite.Ignore => "Null-conditional operators are ignored and treated as regular member access.",
                        NullConditionalRewrite.Rewrite => "Null-conditional operators are rewritten as explicit null checks for better compatibility.",
                        _ => "Default null handling strategy is applied."
                    };

                    membersSb.AppendLine("    /// <para>");
                    membersSb.AppendLine($"    /// Null handling strategy: {nullStrategyDescription}");
                    membersSb.AppendLine("    /// </para>");
                    membersSb.AppendLine("    /// </remarks>");
                    var funcTypeArguments = string.Join(", ", parameterFqns.Append(destFqn));
                    membersSb.AppendLine("    public static Expression<Func<" + funcTypeArguments + ">> " + expressionMethodName + "() => ");
                    var ocePrettyPrinted = PrettyPrinter.Print(inlinedBody, 2);
                    membersSb.Append("        " + lambdaParameters + " => ");
                    membersSb.AppendLine(ocePrettyPrinted + ";");
                }

                // Explicit adaptations
                var adaptationPairs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var adaptation in mm.Adaptations)
                {
                    var generation = adaptation.Generation;
                    var generateMap = (generation & AdaptGeneration.Map) == AdaptGeneration.Map;
                    var generateExpression = (generation & AdaptGeneration.Expression) == AdaptGeneration.Expression;

                    if (generateExpression && string.IsNullOrWhiteSpace(adaptation.GeneratedName))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AdaptExpressionWithoutName,
                            adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name));
                        continue;
                    }

                    var adaptName = string.IsNullOrWhiteSpace(adaptation.GeneratedName) ? mm.Name : adaptation.GeneratedName!;
                    var adaptSourceFqn = TypeDisplay.ForSymbol(adaptation.SourceType, NullableAnnotation.None, mm.SemanticModel.GetNullableContext(nullableContextPosition));
                    var adaptDestFqn = TypeDisplay.ForSymbol(adaptation.DestinationType, NullableAnnotation.None, mm.SemanticModel.GetNullableContext(nullableContextPosition));
                    var additionalParameterListWithNames = string.Join(", ",
                        mm.Parameters.Skip(1).Select(p => $"{TypeDisplay.ForSymbol(p.Type, p.NullableAnnotation, mm.SemanticModel.GetNullableContext(nullableContextPosition))} {p.Name}"));
                    var adaptMethodParametersWithNames = adaptSourceFqn + " " + srcName +
                        (string.IsNullOrEmpty(additionalParameterListWithNames) ? "" : ", " + additionalParameterListWithNames);

                    var adaptPairSignature = BuildMethodSignature("", [adaptSourceFqn, adaptDestFqn]);
                    if (!adaptationPairs.Add(adaptPairSignature))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AdaptDuplicatePair,
                            adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name,
                            adaptation.SourceType.ToDisplayString(),
                            adaptation.DestinationType.ToDisplayString()));
                        continue;
                    }

                    var adaptMapSignature = BuildMethodSignature(adaptName, new[] { adaptSourceFqn }.Concat(parameterFqns.Skip(1)));
                    var adaptExpressionName = adaptName + "Expression";
                    if (generateMap && (existingMethodSignatures.Contains(adaptMapSignature) || !generatedMemberSignatures.Add(adaptMapSignature)))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AdaptNameConflict,
                            adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name,
                            adaptMapSignature));
                        continue;
                    }

                    if (generateExpression && (existingMemberNames.Contains(adaptExpressionName) || !generatedMemberSignatures.Add(adaptExpressionName + "()")))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AdaptNameConflict,
                            adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name,
                            adaptExpressionName));
                        continue;
                    }

                    var adaptInliner = new InliningResolver(mm.SemanticModel, modelsByMethod, false, adaptation.NullStrategy);
                    var inlinedBody = (ExpressionSyntax)adaptInliner.Visit(mm.BodySyntax.Expression)!.WithoutTrivia();
                    allUsingDirectives.UnionWith(adaptInliner.UsingDirectives.Concat(mm.UsingDirectives));

                    if (adaptInliner.CircularReferences.Any())
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AdaptCircularHelper,
                            mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name));
                        continue;
                    }

                    if (!ValidateAdaptation(spc, mm, adaptation, inlinedBody))
                    {
                        continue;
                    }

                    var adaptedBody = RewriteAdaptedBody(inlinedBody, mm.ReturnType, adaptation.DestinationType, adaptDestFqn, mm.SemanticModel);
                    var adaptedBodyText = PrettyPrinter.Print(adaptedBody, 2);

                    if (generateMap)
                    {
                        if (!isFirst)
                        {
                            membersSb.AppendLine();
                        }
                        isFirst = false;

                        membersSb.AppendLine("    /// <summary>");
                        membersSb.AppendLine($"    /// This is an auto-generated adapted mapping method for <see cref=\"{mm.Name}({methodParameterList})\"/>.");
                        membersSb.AppendLine("    /// </summary>");
                        membersSb.AppendLine($"    public static {adaptDestFqn} {adaptName}({adaptMethodParametersWithNames}) =>");
                        membersSb.AppendLine("        " + adaptedBodyText + ";");
                    }

                    if (generateExpression)
                    {
                        if (!isFirst)
                        {
                            membersSb.AppendLine();
                        }
                        isFirst = false;

                        var adaptFuncArgs = string.Join(", ", new[] { adaptSourceFqn }.Concat(parameterFqns.Skip(1)).Append(adaptDestFqn));
                        membersSb.AppendLine("    /// <summary>");
                        membersSb.AppendLine($"    /// This is an auto-generated adapted expression companion for <see cref=\"{mm.Name}({methodParameterList})\"/>.");
                        membersSb.AppendLine("    /// </summary>");
                        membersSb.AppendLine("    public static Expression<Func<" + adaptFuncArgs + ">> " + adaptExpressionName + "() => ");
                        membersSb.Append("        " + lambdaParameters + " => ");
                        membersSb.AppendLine(adaptedBodyText + ";");
                    }
                }

                // Update method - check for circular references like expressive methods do
                if (mm.IsUpdatable)
                {
                    var expressionInliner = new InliningResolver(mm.SemanticModel, modelsByMethod, true, NullConditionalRewrite.None);
                    var inlinedBody = expressionInliner.Visit(mm.BodySyntax.Expression)!.WithoutTrivia();
                    allUsingDirectives.UnionWith(expressionInliner.UsingDirectives.Concat(mm.UsingDirectives));

                    // Skip generating Updatable method if there are circular references
                    if (expressionInliner.CircularReferences.Any())
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.UpdatableCircularReferences,
                            mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name);

                        spc.ReportDiagnostic(diagnostic);
                        continue; // Skip Updatable method generation
                    }

                    // Check if return type is a value type - if so, skip generation and emit warning
                    if (mm.ReturnType.IsValueType && !SymbolHelpers.CanBeNull(mm.ReturnType))
                    {
                        // Emit a diagnostic warning for value type Updatable methods
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.UpdatableValueTypeReturn,
                            mm.MethodSymbol.Locations.FirstOrDefault(),
                            mm.MethodSymbol.Name,
                            mm.ReturnType.ToDisplayString());

                        spc.ReportDiagnostic(diagnostic);

                        // Skip generating the Updatable method
                        continue;
                    }

                    var lines = new List<string>();

                    var replacedMethod = mm.BodySyntax.ReplaceNode(mm.BodySyntax.Expression, inlinedBody);

                    if (EmitHelpers.TryBuildUpdateAssignmentsWithInlining(replacedMethod.Expression, "dest", lines, mm))
                    {
                        var updateMethodName = mm.Name;

                        if (!isFirst)
                        {
                            membersSb.AppendLine();
                        }
                        isFirst = false;

                        membersSb.AppendLine("    /// <summary>");
                        membersSb.AppendLine($"    /// This is an auto-generated update method for <see cref=\"{mm.Name}({methodParameterList})\"/>.");
                        membersSb.AppendLine("    /// </summary>");
                        membersSb.AppendLine($"    /// <param name=\"{srcName}\">The source object to map values from. If null, no updates are performed.</param>");
                        foreach (var parameter in mm.Parameters.Skip(1))
                        {
                            membersSb.AppendLine($"    /// <param name=\"{parameter.Name}\"/>");
                        }
                        membersSb.AppendLine("    /// <param name=\"dest\">The destination object to update. If null, the new instance is created.</param>");
                        membersSb.AppendLine("    /// <returns>The updated destination object for method chaining, or the new destination instance if either parameter is null.</returns>");
                        membersSb.AppendLine("    public static " + destFqn + " " + updateMethodName + "(" + methodParameterListWithNames + ", " + destFqn + " dest)");
                        membersSb.AppendLine("    {");
                        foreach (var l in lines) membersSb.AppendLine("        " + l);
                        membersSb.AppendLine("    }");
                    }
                }
            }

            var sb = new StringBuilder();

            // Always include essential system namespaces that are commonly used in generated code
            allUsingDirectives.UnionWith(["System", "System.Linq", "System.Linq.Expressions", "System.CodeDom.Compiler"]);

            var containingNamespace = mapperType.ContainingNamespace is { IsGlobalNamespace: false } ? mapperType.ContainingNamespace.ToDisplayString() : "";

            // Add using directives to the generated file, filtering out the current namespace
            foreach (var usingDirective in allUsingDirectives.OrderBy(x => x))
            {
                if (usingDirective != containingNamespace && !string.IsNullOrEmpty(usingDirective))
                {
                    sb.AppendLine($"using {usingDirective};");
                }
            }

            if (!string.IsNullOrEmpty(containingNamespace))
            {
                sb.AppendLine();
                sb.AppendLine("namespace " + containingNamespace + ";");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine();
            }

            sb.AppendLine($"[GeneratedCode(\"AlephMapper\", \"{VersionInfo.Version}\")]");
            sb.AppendLine("partial class " + mapperType.Name);
            sb.AppendLine("{");
            sb.Append(membersSb);

            sb.AppendLine("}"); // class

            var fileName = (string.IsNullOrEmpty(containingNamespace)
                               ? ""
                               : containingNamespace.Replace('.', '_') + "_")
                           + mapperType.Name + "_GeneratedMappings.g.cs";

            var output = sb.ToString();
            spc.AddSource(fileName, output);
        }
    }

    private static string GetExpressiveAttributeSource()
    {
        var assembly = typeof(AlephSourceGenerator).Assembly;
        using var streamReader = new StreamReader(assembly.GetManifestResourceStream("AlephMapper.Attributes.cs")!);
        return streamReader.ReadToEnd();
    }

    private static MappingModel GetMappingModel(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not MethodDeclarationSyntax methodDecl) return null;
        if (methodDecl.Parent is not ClassDeclarationSyntax classDecl) return null;

        var classIsStatic = classDecl.Modifiers
            .Any(m => m.IsKind(SyntaxKind.StaticKeyword));

        if (!classIsStatic) return null;

        var model = ctx.SemanticModel;
        var classSymbol = model.GetDeclaredSymbol(classDecl, ct);
        var methodSymbol = model.GetDeclaredSymbol(methodDecl, ct);

        if (classSymbol == null || methodSymbol == null)
        {
            return null;
        }

        if (methodSymbol.Parameters.Length == 0)
        {
            return null;
        }

        var hasExpressive = SymbolHelpers.HasAttribute(classSymbol, typeof(ExpressiveAttribute).FullName)
                            || SymbolHelpers.HasAttribute(methodSymbol, typeof(ExpressiveAttribute).FullName);

        var hasUpdatable = SymbolHelpers.HasAttribute(classSymbol, typeof(UpdatableAttribute).FullName)
                            || SymbolHelpers.HasAttribute(methodSymbol, typeof(UpdatableAttribute).FullName);

        var adaptations = GetAdaptations(methodSymbol);

        var bodyExpr = ExtractBodyExpression(methodDecl);

        if (bodyExpr == null)
        {
            return null;
        }

        var nullStrategy = GetNullStrategy(methodSymbol)
                           ?? GetNullStrategy(classSymbol)
                           ?? NullConditionalRewrite.Ignore;

        var collectionUpdatePolicy = GetCollectionPropertiesPolicy(methodSymbol)
                                     ?? GetCollectionPropertiesPolicy(classSymbol)
                                     ?? CollectionPropertiesPolicy.Skip;

        var isClassPartial = classDecl.Modifiers
                                      .Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        var usingDirectives = ExtractUsingDirectives(methodDecl);

        return new MappingModel(
            classSymbol,
            methodSymbol,
            methodSymbol.Name,
            methodSymbol.Parameters,
            methodSymbol.ReturnType,
            bodyExpr,
            model,
            hasExpressive,
            hasUpdatable,
            isClassPartial,
            nullStrategy,
            collectionUpdatePolicy,
            usingDirectives,
            adaptations
        );
    }

    private static IReadOnlyList<AdaptationModel> GetAdaptations(IMethodSymbol methodSymbol)
    {
        var result = new List<AdaptationModel>();
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != typeof(AdaptAttribute).FullName)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 2 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType ||
                attribute.ConstructorArguments[1].Value is not INamedTypeSymbol destinationType)
            {
                continue;
            }

            string? name = null;
            var generation = AdaptGeneration.Both;
            var nullStrategy = NullConditionalRewrite.Ignore;

            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == nameof(AdaptAttribute.Name))
                {
                    name = named.Value.Value as string;
                }
                else if (named.Key == nameof(AdaptAttribute.Generate) && named.Value.Value is int generationValue)
                {
                    generation = (AdaptGeneration)generationValue;
                }
                else if (named.Key == nameof(AdaptAttribute.NullConditionalRewrite) && named.Value.Value is int nullStrategyValue)
                {
                    nullStrategy = (NullConditionalRewrite)nullStrategyValue;
                }
            }

            result.Add(new AdaptationModel(sourceType, destinationType, name, generation, nullStrategy, attribute));
        }

        return result;
    }

    private static bool ValidateAdaptation(SourceProductionContext spc, MappingModel mapping, AdaptationModel adaptation, ExpressionSyntax body)
    {
        var location = adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? mapping.MethodSymbol.Locations.FirstOrDefault();
        var ok = true;

        if (SymbolEqualityComparer.Default.Equals(adaptation.SourceType, mapping.Parameters[0].Type) &&
            SymbolEqualityComparer.Default.Equals(adaptation.DestinationType, mapping.ReturnType))
        {
            spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidAdaptType, location, mapping.MethodSymbol.Name));
            ok = false;
        }

        if (adaptation.SourceType.IsUnboundGenericType || adaptation.DestinationType.IsUnboundGenericType ||
            adaptation.SourceType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter) ||
            adaptation.DestinationType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
        {
            spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AdaptOpenGenericType, location, mapping.MethodSymbol.Name));
            ok = false;
        }

        foreach (var path in CollectSourceMemberPaths(body, mapping.Parameters[0].Name))
        {
            if (!CanResolveReadablePath(adaptation.SourceType, path))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptSourceMemberMissing,
                    location,
                    mapping.MethodSymbol.Name,
                    string.Join(".", path),
                    adaptation.SourceType.ToDisplayString()));
                ok = false;
            }
        }

        foreach (var memberName in CollectTopLevelDestinationAssignments(body))
        {
            if (!HasWritableInstanceMember(adaptation.DestinationType, memberName))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptDestinationMemberMissing,
                    location,
                    mapping.MethodSymbol.Name,
                    memberName,
                    adaptation.DestinationType.ToDisplayString()));
                ok = false;
            }
        }

        return ok;
    }

    private static IEnumerable<string[]> CollectSourceMemberPaths(ExpressionSyntax body, string sourceParameterName)
    {
        var paths = new List<string[]>();
        foreach (var access in body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            var segments = TryGetMemberPath(access, sourceParameterName);
            if (segments is { Length: > 0 })
            {
                paths.Add(segments);
            }
        }

        return paths
            .GroupBy(p => string.Join(".", p), StringComparer.Ordinal)
            .Select(g => g.First());
    }

    private static string[]? TryGetMemberPath(ExpressionSyntax expression, string sourceParameterName)
    {
        var segments = new Stack<string>();
        ExpressionSyntax current = expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            segments.Push(memberAccess.Name.Identifier.Text);
            current = memberAccess.Expression;
        }

        return current is IdentifierNameSyntax identifier && identifier.Identifier.Text == sourceParameterName
            ? segments.ToArray()
            : null;
    }

    private static bool CanResolveReadablePath(ITypeSymbol rootType, IReadOnlyList<string> path)
    {
        ITypeSymbol currentType = rootType;
        foreach (var segment in path)
        {
            var member = currentType.GetMembers(segment)
                .FirstOrDefault(static m => !m.IsStatic && IsReadableValueMember(m));
            if (member == null)
            {
                return false;
            }

            currentType = GetMemberType(member)!;
            if (currentType == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadableValueMember(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol property => property.GetMethod != null,
            IFieldSymbol field => !field.IsConst,
            _ => false
        };
    }

    private static ITypeSymbol? GetMemberType(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
    }

    private static IEnumerable<string> CollectTopLevelDestinationAssignments(ExpressionSyntax body)
    {
        InitializerExpressionSyntax? initializer = body switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,
            _ => null
        };

        if (initializer == null)
        {
            yield break;
        }

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax identifier)
            {
                yield return identifier.Identifier.Text;
            }
            else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
            {
                yield return memberAccess.Name.Identifier.Text;
            }
        }
    }

    private static bool HasWritableInstanceMember(INamedTypeSymbol type, string memberName)
    {
        return type.GetMembers(memberName).Any(static m => m switch
        {
            IPropertySymbol property => !property.IsStatic && property.SetMethod != null,
            IFieldSymbol field => !field.IsStatic && !field.IsReadOnly && !field.IsConst,
            _ => false
        });
    }

    private static ExpressionSyntax RewriteAdaptedBody(
        ExpressionSyntax body,
        ITypeSymbol originalDestinationType,
        INamedTypeSymbol adaptedDestinationType,
        string adaptedDestinationTypeName,
        SemanticModel semanticModel)
    {
        return (ExpressionSyntax)new AdaptedDestinationRewriter(originalDestinationType, adaptedDestinationTypeName, semanticModel, body)
            .Visit(body)!;
    }

    private sealed class AdaptedDestinationRewriter(
        ITypeSymbol originalDestinationType,
        string adaptedDestinationTypeName,
        SemanticModel semanticModel,
        ExpressionSyntax root) : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var rewritten = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            return IsOriginalDestinationCreation(node)
                ? rewritten.WithType(SyntaxFactory.ParseTypeName(adaptedDestinationTypeName))
                : rewritten;
        }

        public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            var rewritten = (ImplicitObjectCreationExpressionSyntax)base.VisitImplicitObjectCreationExpression(node)!;
            return IsOriginalDestinationCreation(node)
                ? SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName(adaptedDestinationTypeName),
                        rewritten.ArgumentList,
                        rewritten.Initializer)
                    .WithTriviaFrom(rewritten)
                : rewritten;
        }

        private bool IsOriginalDestinationCreation(ExpressionSyntax node)
        {
            try
            {
                var typeInfo = semanticModel.GetTypeInfo(node);
                var type = typeInfo.Type ?? typeInfo.ConvertedType;
                if (type != null)
                {
                    return SymbolEqualityComparer.Default.Equals(type, originalDestinationType);
                }
            }
            catch (ArgumentException)
            {
                // Inlined/re-trivia'd nodes may be detached from the semantic model's tree.
            }

            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                var typeText = objectCreation.Type.ToString();
                return typeText == originalDestinationType.Name ||
                       typeText == originalDestinationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ||
                       typeText == originalDestinationType.ToDisplayString();
            }

            return ReferenceEquals(node, root);
        }
    }

    private static string BuildMethodSignature(string name, IEnumerable<string> parameterTypeNames)
    {
        return name + "(" + string.Join(",", parameterTypeNames.Select(RemoveNullableSignatureMarker)) + ")";
    }

    private static string RemoveNullableSignatureMarker(string typeName)
    {
        return typeName.EndsWith("?", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 1) : typeName;
    }

    private static NullConditionalRewrite? GetNullStrategy(ISymbol sym)
    {
        var attributeValue = SymbolHelpers.GetAttributeArgumentValue(
            sym,
            typeof(ExpressiveAttribute).FullName,
            nameof(ExpressiveAttribute.NullConditionalRewrite));

        if (attributeValue is int intValue)
        {
            return (NullConditionalRewrite)intValue;
        }

        return null;
    }

    private static CollectionPropertiesPolicy? GetCollectionPropertiesPolicy(ISymbol sym)
    {
        var attributeValue = SymbolHelpers.GetAttributeArgumentValue(
            sym,
            typeof(UpdatableAttribute).FullName,
            nameof(UpdatableAttribute.CollectionProperties));

        if (attributeValue is int intValue)
        {
            return (CollectionPropertiesPolicy)intValue;
        }

        return null;
    }

    private static ArrowExpressionClauseSyntax ExtractBodyExpression(MethodDeclarationSyntax mds)
    {
        if (mds.ExpressionBody != null) return mds.ExpressionBody;
        //if (mds.Body == null) return null;
        //foreach (var statement in mds.Body.Statements)
        //{
        //    if (statement is ReturnStatementSyntax rs) return rs.Expression;
        //}
        return null;
    }

    private static IReadOnlyList<string> ExtractUsingDirectives(SyntaxNode node)
    {
        var compilationUnit = node.SyntaxTree.GetRoot() as CompilationUnitSyntax;
        if (compilationUnit == null) return [];

        var usings = new HashSet<string>();

        // Add using directives from compilation unit
        foreach (var usingDirective in compilationUnit.Usings)
        {
            usings.Add(usingDirective.Name.ToString());
        }

        // Add using directives from any namespace declarations
        foreach (var namespaceDeclSyntax in compilationUnit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            foreach (var usingDirective in namespaceDeclSyntax.Usings)
            {
                usings.Add(usingDirective.Name.ToString());
            }
        }

        return usings.OrderBy(x => x).ToList();
    }
}
