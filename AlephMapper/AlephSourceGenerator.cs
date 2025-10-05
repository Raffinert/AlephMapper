using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
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
            ctx.AddSource("ExpressiveAttribute.g.cs", SourceText.From(GetExpressiveAttributeSource(), Encoding.UTF8)));

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
                            sb.AppendLine($"  /// Expression projection for <see cref=\"{mm.Name}({mm.ParamType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})\"/>");
                            sb.AppendLine("  /// </summary>");
                            sb.AppendLine($"  /// <returns>An expression tree representing the logic of {mm.Name}</returns>");
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
                            sb.AppendLine("  /// Update companion generated from " + mm.Name + ".");
                            sb.AppendLine("  /// </summary>");
                            sb.AppendLine("  public static void " + updateMethodName + "(" + srcFqn + " " + srcName + ", " + destFqn + " dest)");
                            sb.AppendLine("  {");
                            sb.AppendLine("    if (" + srcName + " == null || dest == null) return;");
                            foreach (var l in lines) sb.AppendLine("    " + l);
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
        return """
               using System;

               namespace AlephMapper;

               /// <summary>
               /// Configures how null-conditional operators are handled
               /// </summary>
               public enum NullConditionalRewrite
               {
                   /// <summary>
                   /// Don't rewrite null conditional operators (Default behavior).
                   /// Usage of null conditional operators is thereby not allowed
                   /// </summary>
                   None,

                   /// <summary>
                   /// Ignore null-conditional operators in the generated expression tree
                   /// </summary>
                   /// <remarks>
                   /// <c>(A?.B)</c> is rewritten as expression: <c>(A.B)</c>
                   /// </remarks>
                   Ignore,

                   /// <summary>
                   /// Translates null-conditional operators into explicit null checks
                   /// </summary>
                   /// <remarks>
                   /// <c>(A?.B)</c> is rewritten as expression: <c>(A != null ? A.B : null)</c>
                   /// </remarks>
                   Rewrite
               }

               [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
               public sealed class ExpressiveAttribute : Attribute
               {
                   /// <summary>
                   /// Get or set how null-conditional operators are handled
                   /// </summary>
                   public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
               }
               """;
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