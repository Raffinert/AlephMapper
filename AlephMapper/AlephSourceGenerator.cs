using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        var methodInfos = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is MethodDeclarationSyntax,
                transform: (ctx, _) => GetMethodInfo(ctx))
            .Where(m => m != null)
            .Select((m, _) => m)
            .Collect();

        context.RegisterSourceOutput(methodInfos, (spc, source) => GenerateCode(source, spc));
    }

    private class UnifiedMethodInfo
    {
        public CandidateMethodInfo Candidate { get; set; }
        public ClassInfo? Class { get; set; }
    }

    // Transform: gather candidate and class info
    private static UnifiedMethodInfo GetMethodInfo(GeneratorSyntaxContext ctx)
    {
        var method = (MethodDeclarationSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;
        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        if (methodSymbol == null) return null;

        CandidateMethodInfo candidate = null;
        if (methodSymbol.IsStatic && methodSymbol.Parameters.Length == 1)
            candidate = new CandidateMethodInfo { Symbol = methodSymbol, Syntax = method };

        ClassInfo? classInfo = null;
        if (method.Parent is ClassDeclarationSyntax classDecl && classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
            classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
            classDecl.AttributeLists.Count > 0)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            var expressiveAttribute = classSymbol?.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass != null && (attr.AttributeClass.Name == "ExpressiveAttribute" || attr.AttributeClass.Name == "Expressive"));

            if (expressiveAttribute != null)
            {
                var nullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore;
                if (expressiveAttribute.NamedArguments.Any())
                {
                    var nullConditionalArg = expressiveAttribute.NamedArguments
                        .FirstOrDefault(arg => arg.Key == "NullConditionalRewriteSupport");
                    if (nullConditionalArg.Value.Value is int enumValue)
                    {
                        nullConditionalRewriteSupport = (NullConditionalRewriteSupport)enumValue;
                    }
                }
                var methodInfo = TryCreateMethodInfo(method, semanticModel, nullConditionalRewriteSupport);
                if (methodInfo.HasValue)
                {
                    classInfo = new ClassInfo(
                        className: classSymbol.Name,
                        namespaceName: classSymbol.ContainingNamespace != null && !classSymbol.ContainingNamespace.IsGlobalNamespace
                            ? classSymbol.ContainingNamespace.ToDisplayString()
                            : "",
                        methods: ImmutableArray.Create(methodInfo.Value),
                        nullConditionalRewriteSupport: nullConditionalRewriteSupport);
                }
            }
        }
        if (candidate == null && !classInfo.HasValue) return null;
        return new UnifiedMethodInfo { Candidate = candidate, Class = classInfo };
    }

    private static void GenerateCode(ImmutableArray<UnifiedMethodInfo> methodInfos, SourceProductionContext context)
    {
        var candidates = methodInfos
            .Where(x => x.Candidate != null)
            .Select(x => x.Candidate).ToList();

        var classGroups = methodInfos.Select(x => x.Class).OfType<ClassInfo>()
            .GroupBy(c => c.ClassName)
            .Select(g => new ClassInfo(
                g.Key,
                g.First().NamespaceName,
                g.SelectMany(c => c.Methods).ToImmutableArray(),
                g.First().NullConditionalRewriteSupport)).ToList();

        foreach (var classInfo in classGroups)
        {
            var source = GenerateCompanionClassWithCandidates(classInfo, candidates);
            context.AddSource($"{classInfo.ClassName}.Expressions.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    // Helper type for candidate methods
    private class CandidateMethodInfo
    {
        public IMethodSymbol Symbol { get; set; }
        public MethodDeclarationSyntax Syntax { get; set; }
    }

    private static MethodInfo? TryCreateMethodInfo(MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        if (methodSymbol == null)
            return null;

        var sourceParameter = method.ParameterList.Parameters.First();
        var sourceType = semanticModel.GetTypeInfo(sourceParameter.Type).Type?.ToDisplayString();
        var returnType = methodSymbol.ReturnType.ToDisplayString();

        if (string.IsNullOrEmpty(sourceType) || string.IsNullOrEmpty(returnType))
            return null;

        var companionMethodName = method.Identifier.ValueText + "Expression";

        var expression = GenerateExpressionFromMethod(method, sourceParameter.Identifier.ValueText,
            //allMethods, 
            semanticModel, nullConditionalRewriteSupport);

        return new MethodInfo(
            originalName: method.Identifier.ValueText,
            companionName: companionMethodName,
            sourceType: sourceType,
            returnType: returnType,
            expression: expression);
    }

    private static string GenerateExpressionFromMethod(MethodDeclarationSyntax method, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        ExpressionSyntax expression;

        if (method.ExpressionBody != null)
        {
            expression = method.ExpressionBody.Expression;
        }
        else if (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returnStatement && returnStatement.Expression != null)
        {
            expression = returnStatement.Expression;
        }
        else
        {
            // Fallback for more complex methods
            return $"source => new {method.ReturnType}()";
        }

        // Apply null conditional rewriting if needed
        if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
        {
            var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
            expression = (ExpressionSyntax)rewriter.Visit(expression);
        }

        return GenerateExpressionFromSyntax(expression,
            parameterName,
            semanticModel,
            nullConditionalRewriteSupport);
    }

    private static string GenerateExpressionFromSyntax(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return GenerateExpressionFromObjectCreation(objectCreation, parameterName, semanticModel, nullConditionalRewriteSupport);
        }

        // Transform the expression properly to handle method invocations and other cases
        var transformedExpression = TransformExpression(expression, parameterName, semanticModel, nullConditionalRewriteSupport);
        return $"{parameterName} => {transformedExpression}";
    }

    private static string GenerateExpressionFromObjectCreation(ObjectCreationExpressionSyntax objectCreation,
        string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        var sb = new StringBuilder();
        sb.Append($"{parameterName} => new {objectCreation.Type}");

        if (objectCreation.Initializer?.Expressions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("            {");

            var expressions = new List<string>();
            foreach (var expr in objectCreation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var left = assignment.Left.ToString();
                    var right = TransformExpression(assignment.Right, parameterName, semanticModel, nullConditionalRewriteSupport);
                    expressions.Add($"{left} = {right}");
                }
            }

            if (expressions.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < expressions.Count; i++)
                {
                    sb.Append("                ");
                    sb.Append(expressions[i]);
                    if (i < expressions.Count - 1)
                    {
                        sb.AppendLine(",");
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }
                sb.Append("            }");
            }
            else
            {
                sb.Append(" }");
            }
        }
        else
        {
            sb.Append("()");
        }

        return sb.ToString();
    }

    private static string TransformExpression(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        return TransformExpressionInternal(expression, parameterName, semanticModel, nullConditionalRewriteSupport, false);
    }

    private static string TransformExpressionInternal(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport, bool parentRequiresParentheses)
    {
        // Handle binary expressions BEFORE applying null conditional rewriting 
        // This is important because we need to detect if the left side will become a conditional expression
        if (expression is BinaryExpressionSyntax binary)
        {
            // Check if operands will need parentheses after null-conditional rewriting
            var leftNeedsParens = false;
            var rightNeedsParens = false;

            if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None &&
                ContainsNullConditionalOperator(binary.Left) &&
                ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken))
            {
                leftNeedsParens = true;
            }
            else if (binary.Left is ConditionalExpressionSyntax &&
                     ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken))
            {
                leftNeedsParens = true;
            }

            if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None &&
                ContainsNullConditionalOperator(binary.Right) &&
                ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken))
            {
                rightNeedsParens = true;
            }
            else if (binary.Right is ConditionalExpressionSyntax &&
                     ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken))
            {
                rightNeedsParens = true;
            }

            var left = TransformExpressionInternal(binary.Left, parameterName, semanticModel, nullConditionalRewriteSupport, leftNeedsParens);
            var right = TransformExpressionInternal(binary.Right, parameterName, semanticModel, nullConditionalRewriteSupport, rightNeedsParens);
            var operatorToken = binary.OperatorToken.ToString();

            return $"{left} {operatorToken} {right}";
        }

        // Apply null conditional rewriting
        if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
        {
            var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
            expression = (ExpressionSyntax)rewriter.Visit(expression);
        }

        // Handle conditional expressions
        if (expression is ConditionalExpressionSyntax conditional)
        {
            var condition = TransformExpressionInternal(conditional.Condition, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenTrue = TransformExpressionInternal(conditional.WhenTrue, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenFalse = TransformExpressionInternal(conditional.WhenFalse, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);

            var result = $"{condition} ? {whenTrue} : {whenFalse}";

            // Add parentheses if the parent context requires them (for precedence)
            return parentRequiresParentheses ? $"({result})" : result;
        }

        // Handle method invocations (nested mapper calls)
        if (expression is InvocationExpressionSyntax invocation)
        {
            return HandleMethodInvocation(invocation, parameterName, semanticModel, nullConditionalRewriteSupport);
        }

        // Handle member access expressions like source.Property
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var expressionStr = memberAccess.ToString();
            return expressionStr.Replace("source", parameterName);
        }

        // Handle parenthesized expressions
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            var inner = TransformExpressionInternal(parenthesized.Expression, parameterName, semanticModel, nullConditionalRewriteSupport, false);
            return $"({inner})";
        }

        // Handle simple identifiers and other expressions
        var expressionString = expression.ToString();
        return expressionString.Replace("source", parameterName);
    }

    // Helper method to check if an expression contains null-conditional operators
    private static bool ContainsNullConditionalOperator(ExpressionSyntax expression)
    {
        // Simple check for null-conditional operators
        return expression.ToString().Contains("?.")
            || expression.ToString().Contains("?[");
    }

    // Helper method to determine when parentheses are needed around conditional expressions in binary expressions
    private static bool ConditionalNeedsParenthesesInBinaryExpression(SyntaxToken operatorToken)
    {
        // Conditional operator (?:) has the lowest precedence except for assignment and lambda.
        // Therefore ANY use of a conditional expression as an operand of another binary operator
        // needs parentheses so that the conditional does not capture the rest of the expression.
        // We explicitly list operators where we require parentheses around an embedded conditional.
        return operatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||    // ==
               operatorToken.IsKind(SyntaxKind.ExclamationEqualsToken) || // !=
               operatorToken.IsKind(SyntaxKind.LessThanToken) ||          // <
               operatorToken.IsKind(SyntaxKind.LessThanEqualsToken) ||    // <=
               operatorToken.IsKind(SyntaxKind.GreaterThanToken) ||       // >
               operatorToken.IsKind(SyntaxKind.GreaterThanEqualsToken) || // >=
               operatorToken.IsKind(SyntaxKind.PlusToken) ||              // +
               operatorToken.IsKind(SyntaxKind.MinusToken) ||             // -
               operatorToken.IsKind(SyntaxKind.AsteriskToken) ||          // *
               operatorToken.IsKind(SyntaxKind.SlashToken) ||             // /
               operatorToken.IsKind(SyntaxKind.PercentToken) ||           // %
               operatorToken.IsKind(SyntaxKind.QuestionQuestionToken) ||  // ??
               operatorToken.IsKind(SyntaxKind.BarBarToken) ||            // ||
               operatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken);  // &&
    }

    private static MethodDeclarationSyntax GetMethodDeclaration(ExpressionSyntax syntax, SemanticModel semanticModel)
    {
        if (semanticModel.SyntaxTree != syntax.SyntaxTree)
        {
            return null;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(syntax);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is MethodDeclarationSyntax methodDecl)
            {
                return methodDecl;
            }
        }

        return null;
    }

    private static string HandleMethodInvocation(InvocationExpressionSyntax invocation, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {

        // Get the argument being passed to the method
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return invocation.ToString().Replace("source", parameterName);
        }

        var targetMethod = GetMethodDeclaration(invocation, semanticModel);
        if (targetMethod == null)
        {
            return invocation.ToString().Replace("source", parameterName);
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var argumentExpression = TransformExpression(argument.Expression, parameterName, semanticModel, nullConditionalRewriteSupport);

        // Try to inline the method - get the parameter name from the target method
        var targetParameter = targetMethod.ParameterList.Parameters.FirstOrDefault();
        if (targetParameter == null)
        {
            return invocation.ToString().Replace("source", parameterName);
        }

        var targetParameterName = targetParameter.Identifier.ValueText;

        // Get the body of the target method and inline it
        ExpressionSyntax bodyExpression = null;
        if (targetMethod.ExpressionBody != null)
        {
            bodyExpression = targetMethod.ExpressionBody.Expression;
        }
        else if (targetMethod.Body?.Statements.Count == 1 && targetMethod.Body.Statements[0] is ReturnStatementSyntax returnStatement)
        {
            bodyExpression = returnStatement.Expression;
        }

        if (bodyExpression != null)
        {
            // Instead of just doing string replacement, we need to properly transform the inlined expression
            // First, apply null conditional rewriting to the original body
            if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
            {
                var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
                bodyExpression = (ExpressionSyntax)rewriter.Visit(bodyExpression);
            }

            // Then transform the expression with the correct parameter substitution
            var inlinedBody = TransformExpressionInternal(bodyExpression, targetParameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            return inlinedBody.Replace(targetParameterName, argumentExpression);
        }

        // Fallback - don't inline, just replace parameter names
        return invocation.ToString().Replace("source", parameterName);
    }

    // Generate companion class using candidate methods for inlining
    private static string GenerateCompanionClassWithCandidates(ClassInfo classInfo, List<CandidateMethodInfo> candidateMethods)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(classInfo.NamespaceName))
        {
            sb.AppendLine($"namespace {classInfo.NamespaceName};");
            sb.AppendLine();
        }
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine();
        var indent = "";
        sb.AppendLine($"{indent}[GeneratedCode(\"AlephMapper\", \"1.0.0\")]");
        sb.AppendLine($"{indent}public partial class {classInfo.ClassName}");
        sb.AppendLine($"{indent}{{");
        for (int i = 0; i < classInfo.Methods.Length; i++)
        {
            var method = classInfo.Methods[i];
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Expression projection for <see cref=\"{method.OriginalName}({method.SourceType})\"/>");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <returns>An expression tree representing the logic of {method.OriginalName}</returns>");
            sb.AppendLine($"{indent}    public static Expression<Func<{method.SourceType}, {method.ReturnType}>> {method.CompanionName}()");
            sb.AppendLine($"{indent}    {{");
            // Use ExpressionFormatter for formatting
            var formattedExpression = ExpressionFormatter.FormatExpression(method.Expression, $"{indent}        ");
            sb.AppendLine($"{indent}        return {formattedExpression};");
            sb.AppendLine($"{indent}    }}");
            if (i < classInfo.Methods.Length - 1)
            {
                sb.AppendLine();
            }
        }
        sb.AppendLine($"{indent}}}");
        return sb.ToString();
    }

    private static string GetExpressiveAttributeSource()
    {
        return """
               using System;

               namespace AlephMapper;

               /// <summary>
               /// Configures how null-conditional operators are handled
               /// </summary>
               public enum NullConditionalRewriteSupport
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
                   public NullConditionalRewriteSupport NullConditionalRewriteSupport { get; set; } = NullConditionalRewriteSupport.Ignore;
               }
               """;
    }
}

// Value types for caching-friendly data model
public readonly struct ClassInfo : IEquatable<ClassInfo>
{
    public readonly string ClassName;
    public readonly string NamespaceName;
    public readonly ImmutableArray<MethodInfo> Methods;
    public readonly NullConditionalRewriteSupport NullConditionalRewriteSupport;

    public ClassInfo(string className, string namespaceName, ImmutableArray<MethodInfo> methods, NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        ClassName = className;
        NamespaceName = namespaceName;
        Methods = methods;
        NullConditionalRewriteSupport = nullConditionalRewriteSupport;
    }

    public bool Equals(ClassInfo other)
    {
        return ClassName == other.ClassName &&
               NamespaceName == other.NamespaceName &&
               Methods.SequenceEqual(other.Methods) &&
               NullConditionalRewriteSupport == other.NullConditionalRewriteSupport;
    }

    public override bool Equals(object obj)
    {
        return obj is ClassInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ClassName?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (NamespaceName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Methods.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)NullConditionalRewriteSupport;
            return hashCode;
        }
    }
}

public readonly struct MethodInfo : IEquatable<MethodInfo>
{
    public readonly string OriginalName;
    public readonly string CompanionName;
    public readonly string SourceType;
    public readonly string ReturnType;
    public readonly string Expression;

    public MethodInfo(string originalName, string companionName, string sourceType, string returnType, string expression)
    {
        OriginalName = originalName;
        CompanionName = companionName;
        SourceType = sourceType;
        ReturnType = returnType;
        Expression = expression;
    }

    public bool Equals(MethodInfo other)
    {
        return OriginalName == other.OriginalName &&
               CompanionName == other.CompanionName &&
               SourceType == other.SourceType &&
               ReturnType == other.ReturnType &&
               Expression == other.Expression;
    }

    public override bool Equals(object obj)
    {
        return obj is MethodInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = OriginalName?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (CompanionName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (SourceType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (ReturnType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Expression?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}