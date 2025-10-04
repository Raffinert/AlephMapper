using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

        context.RegisterSourceOutput(all, static (spc, arr) =>
        {
            //Debugger.Launch();

            if (arr.Length == 0) return;

            var dict = new Dictionary<IMethodSymbol, MappingModel>(SymbolHelpers.MethodComparer.Instance);
            foreach (var mm in arr)
            {
                dict[SymbolHelpers.Normalize(mm.MethodSymbol)] = mm;
            }

            var byType = new Dictionary<INamedTypeSymbol, List<MappingModel>>(SymbolEqualityComparer.Default);
            foreach (var mm in arr)
            {
                if (!byType.TryGetValue(mm.ContainingType, out var list))
                {
                    list = [];
                    byType.Add(mm.ContainingType, list);
                }
                list.Add(mm);
            }

            foreach (var kvp in byType)
            {
                var mapperType = kvp.Key;
                var methods = kvp.Value;

                if (!methods.Any(m => m.IsExpressive || m.IsReverseExpressive || m.IsReverseUpdatable || m.IsUpdateable))
                {
                    continue;
                }

                var ns = mapperType.ContainingNamespace != null && !mapperType.ContainingNamespace.IsGlobalNamespace ? mapperType.ContainingNamespace.ToDisplayString() : "";
                var sb = new StringBuilder();
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Linq;");
                sb.AppendLine("using System.Linq.Expressions;");
                sb.AppendLine("using System.CodeDom.Compiler;");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(ns))
                {
                    //sb.AppendLine("namespace " + ns + " {");
                    sb.AppendLine("namespace " + ns + ";");
                    sb.AppendLine();
                }

                sb.AppendLine("[GeneratedCode(\"AlephMapper\", \"1.0.0\")]");
                sb.AppendLine("partial class " + mapperType.Name + " {");

                foreach (var mm in methods)
                {
                    var srcName = string.IsNullOrEmpty(mm.ParamName) ? "source" : mm.ParamName;
                    var srcFqn = mm.ParamType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var destFqn = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    //var destMin = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                    var projectionName = mm.Name + "Expression";
                    var reverseProjectionName = mm.Name + "ReverseExpression";
                    var updateName = mm.Name; // keep same
                    var reverseUpdateName = "Reverse" + updateName;

                    // Build inlined body first (model-driven before emission)
                    var resolver = new InliningResolver(mm.SemanticModel, dict);
                    var inlinedBody = (ExpressionSyntax)new CommentRemover().Visit(resolver.Visit(mm.BodySyntax));

                    // PROJECTION
                    if (mm.IsExpressive)
                    {
                        var nullHandledExpression = (ExpressionSyntax)new NullConditionalRewriter(mm.NullStrategy).Visit(inlinedBody);
                        //var formattedExpression = ExpressionFormatter.FormatExpression(nullHandledExpression, "      ");

                        sb.AppendLine("  /// <summary>");
                        sb.AppendLine($"  /// Expression projection for <see cref=\"{mm.Name}({mm.ParamType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})\"/>");
                        sb.AppendLine("  /// </summary>");
                        sb.AppendLine($"  /// <returns>An expression tree representing the logic of {mm.Name}</returns>");
                        sb.AppendLine("  public static Expression<Func<" + srcFqn + ", " + destFqn + ">> " + projectionName + "() => ");
                        sb.AppendLine("      " + srcName + " => " + nullHandledExpression.ToFullString() + ";");
                        sb.AppendLine();
                    }

                    // REVERSE PROJECTION (simple placeholder that swaps direction if it's an object creation)
                    if (mm.IsReverseExpressive)
                    {
                        // naive reverse projection: assume object creation with assignments where right side refers to source
                        var reverseExpr = TryBuildReverseProjection(inlinedBody, srcName);
                        if (reverseExpr != null)
                        {
                            sb.AppendLine("  /// <summary>");
                            sb.AppendLine("  /// Reverse projection generated from " + mm.Name + ".");
                            sb.AppendLine("  /// </summary>");
                            sb.AppendLine("  public static Expression<Func<" + destFqn + ", " + srcFqn + ">> " + reverseProjectionName + "() => ");
                            sb.AppendLine("      d => " + reverseExpr + ";");
                            sb.AppendLine();
                        }
                    }

                    // UPDATE
                    if (mm.IsUpdateable)
                    {
                        var lines = new List<string>();
                        if (EmitHelpers.TryBuildUpdateAssignments((ExpressionSyntax)new CommentRemover().Visit(mm.BodySyntax), "dest", lines))
                        {
                            sb.AppendLine("  /// <summary>");
                            sb.AppendLine("  /// Update companion generated from " + mm.Name + ".");
                            sb.AppendLine("  /// </summary>");
                            sb.AppendLine("  public static void " + updateName + "(" + srcFqn + " " + srcName + ", " + destFqn + " dest)");
                            sb.AppendLine("  {");
                            sb.AppendLine("    if (" + srcName + " == null || dest == null) return;");
                            foreach (var l in lines) sb.AppendLine("    " + l);
                            sb.AppendLine("  }");
                            sb.AppendLine();
                        }
                    }

                    // REVERSE UPDATE (clear+repopulate for collections is left to user types; here we show the creation policy hook)
                    if (mm.IsReverseUpdatable)
                    {
                        sb.AppendLine("  /// <summary>");
                        sb.AppendLine("  /// Reverse update generated from " + mm.Name + " with CreationPolicy=" + mm.ReverseCreationPolicy + ".");
                        sb.AppendLine("  /// </summary>");
                        sb.AppendLine("  public static void " + reverseUpdateName + "(" + destFqn + " dest, " + srcFqn + " " + srcName + ")");
                        sb.AppendLine("  {");
                        sb.AppendLine("    if (dest == null) return;");
                        sb.AppendLine("    if (" + srcName + " == null) {");
                        sb.AppendLine("       // CreationPolicy handling");
                        sb.AppendLine("       if (" + mm.ReverseCreationPolicy + " == (int)Projectable.ReverseUpdateCreationPolicy.SkipIfDestNull) return;");
                        sb.AppendLine("       if (" + mm.ReverseCreationPolicy + " == (int)Projectable.ReverseUpdateCreationPolicy.GuardedCreate) return;");
                        sb.AppendLine("       return;");
                        sb.AppendLine("    }");
                        sb.AppendLine("    // TODO: implement reverse field mapping if needed (mirror of forward assignments).");
                        sb.AppendLine("  }");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("}"); // class

                //if (!string.IsNullOrEmpty(ns))
                //{
                //    sb.AppendLine();
                //    sb.AppendLine("}"); // namespace
                //}

                var fileName = (string.IsNullOrEmpty(ns) ? "" : ns.Replace('.', '_') + "_") + mapperType.Name + "_GeneratedMappings.g.cs";

                // Format the generated code using the extracted formatter
                var formattedCode = CodeFormatter.FormatGeneratedCode(sb.ToString());

                spc.AddSource(fileName, formattedCode);
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
    
    private static string TryBuildReverseProjection(ExpressionSyntax inlinedBody, string srcName)
    {
        // Very naive reverse: if body is "new D { P = <src.Prop>, ... }" -> reverse "new S { Prop = d.P, ... }" is not generally possible without type info.
        // Provide a minimal stub: if new expression exists, return identity of source as placeholder.
        var oce = inlinedBody as ObjectCreationExpressionSyntax;
        if (oce == null) return null;
        // Fallback: return "default(S)" - generator users can opt-out if not desired.
        return "default(" + srcName + ")";
    }

    private static MappingModel Transform(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        if (ctx.Node is not MethodDeclarationSyntax methodDecl) return null;
        if (methodDecl.Parent is not ClassDeclarationSyntax classDecl) return null;

        var model = ctx.SemanticModel;
        var classSymbol = model.GetDeclaredSymbol(classDecl, ct);
        var methodSymbol = model.GetDeclaredSymbol(methodDecl, ct);
        if (classSymbol == null || methodSymbol == null) return null;

        if (methodSymbol.Parameters.Length != 1) return null;

        var hasProj = HasAttr(classSymbol, "AlephMapper.ExpressiveAttribute") || HasAttr(methodSymbol, "AlephMapper.ExpressiveAttribute");
        var hasUpd = HasAttr(classSymbol, "AlephMapper.UpdateableAttribute") || HasAttr(methodSymbol, "AlephMapper.UpdateableAttribute");
        var hasRevProj = HasAttr(classSymbol, "AlephMapper.ReverseExpressiveAttribute") || HasAttr(methodSymbol, "AlephMapper.ReverseExpressiveAttribute");
        var hasRevUpd = HasAttr(classSymbol, "AlephMapper.ReverseUpdatableAttribute") || HasAttr(methodSymbol, "AlephMapper.ReverseUpdatableAttribute");

        //if (!(hasProj || hasUpd || hasRevProj || hasRevUpd))
        //    return null;

        var bodyExpr = ExtractBodyExpression(methodDecl);
        if (bodyExpr == null) return null;

        NullConditionalRewrite nullStrategy = GetNullStrategy(methodSymbol) ?? GetNullStrategy(classSymbol) ?? NullConditionalRewrite.Ignore;
        int creationPolicy = GetReverseCreationPolicy(methodSymbol) ?? GetReverseCreationPolicy(classSymbol) ?? 0;

        return new MappingModel(
            classSymbol,
            methodSymbol,
            methodSymbol.Name,
            methodSymbol.Parameters[0].Name,
            methodSymbol.Parameters[0].Type,
            methodSymbol.ReturnType,
            bodyExpr,
            model,
            hasProj,
            hasUpd,
            hasRevProj,
            hasRevUpd,
            nullStrategy,
            creationPolicy
        );
    }

    //private static bool HasAttr(ISymbol sym, string fullName)
    //{
    //    foreach (var a in sym.GetAttributes())
    //    {
    //        var cls = a.AttributeClass;
    //        if (cls != null)
    //        {
    //            var s = cls.ToDisplayString();
    //            if (s == fullName || s == fullName.Substring(fullName.LastIndexOf('.') + 1)) return true;
    //        }
    //    }
    //    return false;
    //}

    private static bool HasAttr(ISymbol sym, string fullName)
    {
        foreach (var a in sym.GetAttributes())
        {
            var cls = a.AttributeClass;
            if (cls != null)
            {
                if (cls.Name == fullName || cls.Name == fullName.Substring(fullName.LastIndexOf('.') + 1)) return true;
            }
        }
        return false;
    }

    private static NullConditionalRewrite? GetNullStrategy(ISymbol sym)
    {
        foreach (var a in sym.GetAttributes())
        {
            var cls = a.AttributeClass;
            var name = cls?.ToDisplayString();
            if (name is not ("AlephMapper.ExpressiveAttribute" or "ExpressiveAttribute"))
            {
                continue;
            }

            foreach (var arg in a.NamedArguments)
            {
                if (arg is { Key: "NullConditionalRewrite", Value.Value: int value })
                    return (NullConditionalRewrite)value;
            }
        }
        return null;
    }

    private static int? GetReverseCreationPolicy(ISymbol sym)
    {
        foreach (var a in sym.GetAttributes())
        {
            var cls = a.AttributeClass;
            if (cls == null) continue;
            var name = cls.ToDisplayString();
            if (name is not ("AlephMapper.ReverseUpdatableAttribute" or "ReverseUpdatableAttribute"))
            {
                continue;
            }

            foreach (var arg in a.NamedArguments)
            {
                if (arg is { Key: "CreationPolicy", Value.Value: int })
                    return (int)arg.Value.Value;
            }
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