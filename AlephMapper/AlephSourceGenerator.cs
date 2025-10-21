using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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

                var nameSpace = mapperType.ContainingNamespace != null && !mapperType.ContainingNamespace.IsGlobalNamespace ? mapperType.ContainingNamespace.ToDisplayString() : "";
                var sb = new StringBuilder();
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Linq;");
                sb.AppendLine("using System.Linq.Expressions;");
                sb.AppendLine("using System.CodeDom.Compiler;");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(nameSpace))
                {
                    sb.AppendLine("namespace " + nameSpace + ";");
                    sb.AppendLine();
                }

                sb.AppendLine($"[GeneratedCode(\"AlephMapper\", \"{VersionInfo.Version}\")]");
                sb.AppendLine("partial class " + mapperType.Name + " {");

                foreach (var mm in methods)
                {
                    var srcName = string.IsNullOrEmpty(mm.ParamName) ? "source" : mm.ParamName;
                    var srcFqn = mm.ParamType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var destFqn = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    if (!mm.IsExpressive && !mm.IsUpdatable) continue;

                    var resolver = new InliningResolver(mm.SemanticModel, modelsByMethod);
                    var inlinedBody = (ExpressionSyntax)new CommentRemover().Visit(resolver.Visit(mm.BodySyntax.Expression));
                    
                    //var originalCompilation = mm.SemanticModel.Compilation;
                    //var newTree = CSharpSyntaxTree.Create(inlinedBody);
                    //var reboundCompilation = originalCompilation.ReplaceSyntaxTree(oldTree, newTree);
                    //var newModel = reboundCompilation.GetSemanticModel(newTree);

                    // Check for circular references and emit warnings
                    foreach (var circularRef in resolver.CircularReferences)
                    {
                        var descriptor = new DiagnosticDescriptor(
                            "AM0002",
                            "Circular reference detected in method inlining",
                            "Circular reference detected in method '{0}'. Call chain: {1}. The circular method call will not be inlined to prevent infinite recursion.",
                            "AlephMapper",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true);

                        var diagnostic = Diagnostic.Create(
                            descriptor,
                            mm.MethodSymbol.Locations.FirstOrDefault(),
                            circularRef.Method.Name,
                            circularRef.CallChain);

                        spc.ReportDiagnostic(diagnostic);
                    }

                    var collectionRewriter = new CollectionExpressionRewriter(mm.SemanticModel);
                    var collectionRewrittenExpression = (ExpressionSyntax)collectionRewriter.Visit(inlinedBody)!.WithoutTrivia();

                    // Expression method
                    if (mm.IsExpressive)
                    {
                        // Skip generating expression method if there are circular references
                        if (resolver.CircularReferences.Any())
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "AM0003",
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

                        // Build inlined body for expressions
                        var nullHandledExpression = (ExpressionSyntax)new NullConditionalRewriter(mm.NullStrategy).Visit(collectionRewrittenExpression)!.WithoutTrivia();

                        var expressionMethodName = mm.Name + "Expression";

                        sb.AppendLine("  /// <summary>");
                        sb.AppendLine($"  /// This is an auto-generated expression companion for <see cref=\"{mm.Name}({mm.ParamType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})\"/>.");
                        sb.AppendLine("  /// </summary>");
                        sb.AppendLine("  /// <remarks>");

                        // Add null strategy information
                        string nullStrategyDescription = mm.NullStrategy switch
                        {
                            NullConditionalRewrite.None => "Null-conditional operators are preserved as-is in the expression tree.",
                            NullConditionalRewrite.Ignore => "Null-conditional operators are ignored and treated as regular member access.",
                            NullConditionalRewrite.Rewrite => "Null-conditional operators are rewritten as explicit null checks for better compatibility.",
                            _ => "Default null handling strategy is applied."
                        };

                        sb.AppendLine("  /// <para>");
                        sb.AppendLine($"  /// Null handling strategy: {nullStrategyDescription}");
                        sb.AppendLine("  /// </para>");
                        sb.AppendLine("  /// </remarks>");
                        sb.AppendLine("  public static Expression<Func<" + srcFqn + ", " + destFqn + ">> " + expressionMethodName + "() => ");
                        sb.AppendLine("      " + srcName + " => " + nullHandledExpression.ToFullString() + ";");
                        sb.AppendLine();
                    }

                    // Update method - check for circular references like expressive methods do
                    if (mm.IsUpdatable)
                    {
                        // Skip generating Updatable method if there are circular references
                        if (resolver.CircularReferences.Any())
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "AM0004",
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

                        var replacedMethod = mm.BodySyntax.ReplaceNode(mm.BodySyntax.Expression, collectionRewrittenExpression);

                        if (mm.SemanticModel.TryGetSpeculativeSemanticModel(
                                position: mm.BodySyntax.SpanStart, // an anchor inside the original tree
                                replacedMethod,        // the rewritten subtree (member/statement/expression)
                                out var specModel) && EmitHelpers.TryBuildUpdateAssignmentsWithInlining(replacedMethod.Expression, "dest", lines, specModel))
                        {
                            var updateMethodName = mm.Name;

                            sb.AppendLine("  /// <summary>");
                            sb.AppendLine($"  /// Updates an existing instance of <see cref=\"{mm.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}\"/> with values from the source object.");
                            sb.AppendLine("  /// </summary>");
                            sb.AppendLine($"  /// <param name=\"{srcName}\">The source object to map values from. If null, no updates are performed.</param>");
                            sb.AppendLine("  /// <param name=\"dest\">The destination object to update. If null, no updates are performed.</param>");
                            sb.AppendLine("  /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>");
                            sb.AppendLine("  public static " + destFqn + " " + updateMethodName + "(" + srcFqn + " " + srcName + ", " + destFqn + " dest)");
                            sb.AppendLine("  {");

                            // Build null check conditions
                            var nullCheckConditions = new List<string>();

                            // Only check source for null if it can be null
                            if (SymbolHelpers.CanBeNull(mm.ParamType))
                            {
                                nullCheckConditions.Add($"{srcName} == null");
                            }

                            // Only check destination for null if it can be null
                            if (SymbolHelpers.CanBeNull(mm.ReturnType))
                            {
                                nullCheckConditions.Add("dest == null");
                            }

                            // Generate null check only if there are conditions to check
                            if (nullCheckConditions.Count > 0)
                            {
                                sb.AppendLine("    if (" + string.Join(" || ", nullCheckConditions) + ") return dest;");
                            }

                            foreach (var l in lines) sb.AppendLine("    " + l);
                            sb.AppendLine("    return dest;");
                            sb.AppendLine("  }");
                            sb.AppendLine();
                        }
                    }
                }

                sb.AppendLine("}"); // class

                var fileName = (string.IsNullOrEmpty(nameSpace)
                    ? ""
                    : nameSpace.Replace('.', '_') + "_")
                        + mapperType.Name + "_GeneratedMappings.g.cs";

                //var formattedCode = CodeFormatter.FormatGeneratedCode(sb.ToString());
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

        var isClassPartial = classDecl.Modifiers
                                      .Any(m => m.IsKind(SyntaxKind.PartialKeyword));

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
            nullStrategy
        );
    }

    private static NullConditionalRewrite? GetNullStrategy(ISymbol sym)
    {
        var attributeValue = SymbolHelpers.GetAttributeArgumentValue(
            sym,
            typeof(ExpressiveAttribute).FullName,
            nameof(NullConditionalRewrite));

        if (attributeValue is int intValue)
        {
            return (NullConditionalRewrite)intValue;
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
}