using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace AlephMapper;

[Generator]
public class AlephSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the attribute source
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("AlephMapper.Attributes.g.cs", SourceText.From(GetExpressiveAttributeSource(), Encoding.UTF8)));

        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is MethodDeclarationSyntax && node.Parent is ClassDeclarationSyntax,
            Transform
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

                if (!methods.Any(m => m.IsExpressive || m.IsUpdateable))
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

                sb.AppendLine("[GeneratedCode(\"AlephMapper\", \"1.0.0\")]");
                sb.AppendLine("partial class " + mapperType.Name + " {");

                foreach (var mm in methods)
                {
                    var srcName = string.IsNullOrEmpty(mm.ParamName) ? "source" : mm.ParamName;
                    var srcFqn = mm.ParamType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var destFqn = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Build inlined body first (model-driven before emission)
                    var resolver = new InliningResolver(mm.SemanticModel, modelsByMethod);
                    var inlinedBody = (ExpressionSyntax)new CommentRemover().Visit(resolver.Visit(mm.BodySyntax));

                    // Expression method
                    if (mm.IsExpressive)
                    {
                        var nullHandledExpression = (ExpressionSyntax)new NullConditionalRewriter(mm.NullStrategy).Visit(inlinedBody);

                        if (nullHandledExpression != null)
                        {
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
                    }

                    // Update method
                    if (mm.IsUpdateable)
                    {
                        var lines = new List<string>();
                        if (EmitHelpers.TryBuildUpdateAssignments((ExpressionSyntax)new CommentRemover().Visit(mm.BodySyntax), "dest", lines))
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
                            sb.AppendLine("    if (" + srcName + " == null || dest == null) return dest;");
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

                // Format the generated code using the extracted formatter
               // var formattedCode = CodeFormatter.FormatGeneratedCode(sb.ToString());

                spc.AddSource(fileName, sb.ToString());
            }
        });
    }

    private static string GetExpressiveAttributeSource()
    {
        var assembly = typeof(AlephSourceGenerator).Assembly;
        using var streamReader =  new StreamReader(assembly.GetManifestResourceStream("AlephMapper.Attributes.cs")!);
        return streamReader.ReadToEnd();
    }
    
    private static MappingModel Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not MethodDeclarationSyntax methodDecl) return null;
        if (methodDecl.Parent is not ClassDeclarationSyntax classDecl) return null;

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

        var hasExpressive = SymbolHelpers.HasAttribute(classSymbol, "AlephMapper.ExpressiveAttribute") 
                            || SymbolHelpers.HasAttribute(methodSymbol, "AlephMapper.ExpressiveAttribute");
        
        var hasUpdateable = SymbolHelpers.HasAttribute(classSymbol, "AlephMapper.UpdateableAttribute") 
                            || SymbolHelpers.HasAttribute(methodSymbol, "AlephMapper.UpdateableAttribute");

        var bodyExpr = ExtractBodyExpression(methodDecl);
        
        if (bodyExpr == null)
        {
            return null;
        }

        var nullStrategy = GetNullStrategy(methodSymbol) ?? GetNullStrategy(classSymbol) ?? NullConditionalRewrite.Ignore;
        
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
            hasUpdateable,
            nullStrategy
        );
    }
    

    private static NullConditionalRewrite? GetNullStrategy(ISymbol sym)
    {
        var attributeValue = SymbolHelpers.GetAttributeArgumentValue(
            sym, 
            "AlephMapper.ExpressiveAttribute", 
            "NullConditionalRewrite");

        if (attributeValue is int intValue)
        {
            return (NullConditionalRewrite)intValue;
        }
        
        return null;
    }

    private static ExpressionSyntax ExtractBodyExpression(MethodDeclarationSyntax mds)
    {
        if (mds.ExpressionBody != null) return mds.ExpressionBody.Expression;
        if (mds.Body == null) return null;
        foreach (var statement in mds.Body.Statements)
        {
            if (statement is ReturnStatementSyntax rs) return rs.Expression;
        }
        return null;
    }
}