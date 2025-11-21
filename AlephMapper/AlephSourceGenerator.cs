using AlephMapper.Helpers;
using AlephMapper.Models;
using AlephMapper.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

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
            if (models.Length == 0) return;

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

                if (!methods.Any(m => (m.IsExpressive || m.IsUpdatable) && m.IsClassPartial))
                {
                    continue;
                }

                var membersSb = new StringBuilder();

                var allUsingDirectives = new HashSet<string>();

                bool isFirst = true;

                foreach (var mm in methods)
                {
                    var srcName = mm.ParamName;
                    var srcFqn = mm.ParamType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    var destFqn = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                    if (!mm.IsExpressive && !mm.IsUpdatable) continue;

                    // Expression method
                    if (mm.IsExpressive)
                    {
                        var expressionInliner = new InliningResolver(mm.SemanticModel, modelsByMethod, false, mm.NullStrategy);
                        var inlinedBody = expressionInliner.Visit(mm.BodySyntax.Expression)!.WithoutTrivia();
                        allUsingDirectives.UnionWith(expressionInliner.UsingDirectives.Concat(mm.UsingDirectives));

                        // Skip generating expression method if there are circular references
                        if (expressionInliner.CircularReferences.Any())
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "AM0002",
                                "Expressive method generation skipped due to circular references",
                                "Expression method generation skipped for '{0}' due to circular references. Fix the circular dependencies to enable expression generation.",
                                "AlephMapper",
                                DiagnosticSeverity.Warning,
                                isEnabledByDefault: true);

                            var diagnostic = Diagnostic.Create(
                                descriptor,
                                mm.MethodSymbol.Locations.FirstOrDefault(),
                                mm.MethodSymbol.Name);

                            spc.ReportDiagnostic(diagnostic);
                            continue; // Skip expression generation
                        }

                        var expressionMethodName = mm.Name + "Expression";

                        if(!isFirst)
                        {
                            membersSb.AppendLine();
                        }
                        isFirst = false;
                        membersSb.AppendLine("    /// <summary>");
                        membersSb.AppendLine($"    /// This is an auto-generated expression companion for <see cref=\"{mm.Name}({mm.ParamType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})\"/>.");
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
                        membersSb.AppendLine("    public static Expression<Func<" + srcFqn + ", " + destFqn + ">> " + expressionMethodName + "() => ");
                        var ocePrettyPrinted = PrettyPrinter.Print(inlinedBody, 2);
                        membersSb.Append("        " + srcName + " => ");
                        membersSb.AppendLine(ocePrettyPrinted + ";");                        
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
                            var descriptor = new DiagnosticDescriptor(
                                "AM0003",
                                "Updatable method generation skipped due to circular references",
                                "Updatable method generation skipped for '{0}' due to circular references. Fix the circular dependencies to enable Updatable method generation.",
                                "AlephMapper",
                                DiagnosticSeverity.Warning,
                                isEnabledByDefault: true);

                            var diagnostic = Diagnostic.Create(
                                descriptor,
                                mm.MethodSymbol.Locations.FirstOrDefault(),
                                mm.MethodSymbol.Name);

                            spc.ReportDiagnostic(diagnostic);
                            continue; // Skip Updatable method generation
                        }

                        // Check if return type is a value type - if so, skip generation and emit warning
                        if (mm.ReturnType.IsValueType && !SymbolHelpers.CanBeNull(mm.ReturnType))
                        {
                            // Emit a diagnostic warning for value type Updatable methods
                            var descriptor = new DiagnosticDescriptor(
                                "AM0001",
                                "Updatable method with value type return type",
                                "Updatable method '{0}' returns value type '{1}'. Value types are passed by value, so update semantics don't work as expected. Consider using a regular mapping method instead.",
                                "AlephMapper",
                                DiagnosticSeverity.Warning,
                                isEnabledByDefault: true);

                            var diagnostic = Diagnostic.Create(
                                descriptor,
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

                            if(!isFirst)
                            {
                                membersSb.AppendLine();
                            }
                            isFirst = false;

                            membersSb.AppendLine("    /// <summary>");
                            membersSb.AppendLine($"    /// Updates an existing instance of <see cref=\"{mm.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}\"/> with values from the source object.");
                            membersSb.AppendLine("    /// </summary>");
                            membersSb.AppendLine($"    /// <param name=\"{srcName}\">The source object to map values from. If null, no updates are performed.</param>");
                            membersSb.AppendLine("    /// <param name=\"dest\">The destination object to update. If null, no updates are performed.</param>");
                            membersSb.AppendLine("    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>");
                            membersSb.AppendLine("    public static " + destFqn + " " + updateMethodName + "(" + srcFqn + " " + srcName + ", " + destFqn + " dest)");
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

                sb.AppendLine();

                if (!string.IsNullOrEmpty(containingNamespace))
                {
                    sb.AppendLine();
                    sb.AppendLine("namespace " + containingNamespace + ";");
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

                spc.AddSource(fileName, sb.ToString());
            }
        });
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

        if (methodSymbol.Parameters.Length != 1)
        {
            return null;
        }

        var hasExpressive = SymbolHelpers.HasAttribute(classSymbol, typeof(ExpressiveAttribute).FullName)
                            || SymbolHelpers.HasAttribute(methodSymbol, typeof(ExpressiveAttribute).FullName);

        var hasUpdatable = SymbolHelpers.HasAttribute(classSymbol, typeof(UpdatableAttribute).FullName)
                            || SymbolHelpers.HasAttribute(methodSymbol, typeof(UpdatableAttribute).FullName);

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
            methodSymbol.Parameters[0].Name,
            methodSymbol.Parameters[0].Type,
            methodSymbol.ReturnType,
            bodyExpr,
            model,
            hasExpressive,
            hasUpdatable,
            isClassPartial,
            nullStrategy,
            collectionUpdatePolicy,
            usingDirectives
        );
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
