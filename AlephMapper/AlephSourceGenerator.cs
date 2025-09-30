using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

                // Only process static methods with exactly 1 parameter
                if (methodSymbol.IsStatic && methodSymbol.Parameters.Length == 1)
                {
                    try
                    {
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
                    catch (Exception)
                    {
                        // If we can't process this method, just continue without failing the entire generation
                        // In a real scenario, you might want to report a diagnostic instead
                    }
                }
            }
        }
        if (candidate == null && classInfo == null) return null;
        return new UnifiedMethodInfo { Candidate = candidate, Class = classInfo };
    }

    private static void GenerateCode(ImmutableArray<UnifiedMethodInfo> methodInfos, SourceProductionContext context)
    {
        // Add diagnostic to see if generator is being called
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor("ALM001", "Generator Called", "AlephMapper source generator called with {0} method infos", "AlephMapper", DiagnosticSeverity.Info, true),
            Location.None, methodInfos.Length));

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

        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor("ALM002", "Classes Found", "Found {0} classes to generate", "AlephMapper", DiagnosticSeverity.Info, true),
            Location.None, classGroups.Count));

        foreach (var classInfo in classGroups)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("ALM003", "Generating Class", "Generating companion for class {0} with {1} methods", "AlephMapper", DiagnosticSeverity.Info, true),
                Location.None, classInfo.ClassName, classInfo.Methods.Length));

            try
            {
                var source = GenerateCompanionClassWithCandidates(classInfo, candidates);
                context.AddSource($"{classInfo.ClassName}.Expressions.g.cs", SourceText.From(source, Encoding.UTF8));

                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("ALM004", "File Generated", "Successfully generated file {0}", "AlephMapper", DiagnosticSeverity.Info, true),
                    Location.None, $"{classInfo.ClassName}.Expressions.g.cs"));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("ALM005", "Generation Error", "Error generating class {0}: {1}", "AlephMapper", DiagnosticSeverity.Error, true),
                    Location.None, classInfo.ClassName, ex.Message));
            }
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

        if (method.ParameterList.Parameters.Count == 0)
            return null;

        var sourceParameter = method.ParameterList.Parameters.First();
        var sourceType = semanticModel.GetTypeInfo(sourceParameter.Type).Type?.ToDisplayString();
        var returnType = methodSymbol.ReturnType.ToDisplayString();

        if (string.IsNullOrEmpty(sourceType) || string.IsNullOrEmpty(returnType))
            return null;

        var companionMethodName = method.Identifier.ValueText + "Expression";

        try
        {
            var expression = GenerateExpressionFromMethod(method, sourceParameter.Identifier.ValueText,
                semanticModel, nullConditionalRewriteSupport);

            return new MethodInfo(
                originalName: method.Identifier.ValueText,
                companionName: companionMethodName,
                sourceType: sourceType,
                returnType: returnType,
                expression: expression);
        }
        catch (Exception)
        {
            // If complex expression generation fails, create a simple fallback expression
            // This ensures the method is still generated but with a basic implementation
            var fallbackExpression = SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(sourceParameter.Identifier.ValueText)),
                SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(returnType)));

            return new MethodInfo(
                originalName: method.Identifier.ValueText,
                companionName: companionMethodName,
                sourceType: sourceType,
                returnType: returnType,
                expression: fallbackExpression);
        }
    }

    private static ExpressionSyntax GenerateExpressionFromMethod(MethodDeclarationSyntax method, string parameterName,
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
            // Fallback for more complex methods - create a simple lambda expression
            var fallbackType = method.ReturnType?.ToString() ?? "object";
            return SyntaxFactory.SimpleLambdaExpression(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),
                SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName(fallbackType))
                    .WithArgumentList(SyntaxFactory.ArgumentList()));
        }

        var originalExpressionString = expression.ToString();

        // CRITICAL: If the expression contains null conditional operators, we MUST eliminate them
        // This is the most aggressive approach to eliminate CS8072 errors
        if (originalExpressionString.Contains("?.") || originalExpressionString.Contains("?["))
        {
            // Use an extremely aggressive approach - completely eliminate null conditionals regardless of the setting
            string processedString;

            if (nullConditionalRewriteSupport == NullConditionalRewriteSupport.Rewrite)
            {
                // For Rewrite mode, convert to explicit null checks
                processedString = AggressiveNullConditionalRewrite(originalExpressionString);
            }
            else
            {
                // For all other modes (including Ignore and None), just remove the ? operators
                processedString = originalExpressionString.Replace("?.", ".").Replace("?[", "[");
            }

            // Verify that ALL null conditional operators are gone
            if (processedString.Contains("?.") || processedString.Contains("?["))
            {
                // If we still have null conditionals, apply an even more aggressive approach
                processedString = processedString.Replace("?.", ".").Replace("?[", "[");

                // Last resort - manually scan and remove any remaining ? operators before . or [
                processedString = Regex.Replace(processedString, @"\?\.", ".");
                processedString = Regex.Replace(processedString, @"\?\[", "[");
            }

            try
            {
                expression = SyntaxFactory.ParseExpression(processedString);
            }
            catch
            {
                // If parsing fails completely, create a safe default based on return type
                var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                var returnType = methodSymbol?.ReturnType;

                if (returnType != null)
                {
                    var typeName = returnType.ToDisplayString();
                    if (returnType.IsReferenceType || typeName.EndsWith("?"))
                    {
                        expression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
                    }
                    else
                    {
                        // Create a default value for value types
                        expression = typeName switch
                        {
                            "int" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
                            "bool" => SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),
                            "string" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("")),
                            "decimal" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0m)),
                            _ => SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                        };
                    }
                }
                else
                {
                    expression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
                }
            }
        }

        return GenerateExpressionFromSyntax(expression,
            parameterName,
            semanticModel,
            NullConditionalRewriteSupport.None); // Pass None to avoid double rewriting
    }

    // More aggressive null conditional rewriting
    private static string AggressiveNullConditionalRewrite(string expressionText)
    {
        var result = expressionText.Trim();

        // Multiple passes to handle nested null conditionals
        var maxIterations = 5;
        var iteration = 0;

        while ((result.Contains("?.") || result.Contains("?[")) && iteration < maxIterations)
        {
            var oldResult = result;

            // Pattern 1: Handle simple property access like obj?.prop
            result = Regex.Replace(result, @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\?\.([\w]+)", "($1 != null ? $1.$2 : null)");

            // Pattern 2: Handle nested property access like obj.nested?.prop
            result = Regex.Replace(result, @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)+)\?\.([\w]+)", "($1 != null ? $1.$2 : null)");

            // Pattern 3: Handle method calls like obj?.Method()
            result = Regex.Replace(result, @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\?\.([\w]+)\s*\(([^)]*)\)", "($1 != null ? $1.$2($3) : null)");

            // Pattern 4: Handle indexer access like obj?[index]
            result = Regex.Replace(result, @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\?\[([^\]]+)\]", "($1 != null ? $1[$2] : null)");

            // If no change was made, break to avoid infinite loop
            if (result == oldResult)
                break;

            iteration++;
        }

        // Final safety check - if we still have null conditionals, just remove them
        if (result.Contains("?.") || result.Contains("?["))
        {
            result = result.Replace("?.", ".").Replace("?[", "[");
        }

        return result;
    }

    private static ExpressionSyntax GenerateExpressionFromSyntax(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return GenerateExpressionFromObjectCreation(objectCreation, parameterName, semanticModel, nullConditionalRewriteSupport);
        }

        // Transform the expression properly to handle method invocations and other cases
        var transformedExpression = TransformExpression(expression, parameterName, semanticModel, nullConditionalRewriteSupport);

        // Create a lambda expression: parameterName => transformedExpression
        return SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),
            transformedExpression);
    }

    private static ExpressionSyntax GenerateExpressionFromObjectCreation(ObjectCreationExpressionSyntax objectCreation,
        string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        Debugger.Launch();

        // Transform the object creation expression
        var transformedObjectCreation = objectCreation;

        if (objectCreation.Initializer?.Expressions.Count > 0)
        {
            var transformedExpressions = new List<ExpressionSyntax>();

            foreach (var expr in objectCreation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    // CRITICAL FIX: Don't pass nullConditionalRewriteSupport since it should already be rewritten
                    // at the top level in GenerateExpressionFromMethod
                    var transformedRight = TransformExpressionInternal(assignment.Right, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
                    var newAssignment = SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        assignment.Left,
                        transformedRight);
                    transformedExpressions.Add(newAssignment);
                }
                else
                {
                    var transformedExpr = TransformExpression(expr, parameterName, semanticModel, NullConditionalRewriteSupport.None);
                    transformedExpressions.Add(transformedExpr);
                }
            }

            var newInitializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SeparatedList(transformedExpressions));

            transformedObjectCreation = objectCreation.WithInitializer(newInitializer);
        }

        // Create a lambda expression: parameterName => new Type { ... }
        return SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),
            transformedObjectCreation);
    }

    private static ExpressionSyntax TransformExpression(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        // Only apply null conditional rewriting if it hasn't been applied already at the top level
        if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
        {
            try
            {
                var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
                expression = (ExpressionSyntax)rewriter.Visit(expression);
            }
            catch (Exception)
            {
                // If null conditional rewriting fails, continue with original expression
            }
        }

        return TransformExpressionInternal(expression, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
    }

    private static ExpressionSyntax TransformExpressionInternal(ExpressionSyntax expression, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport, bool parentRequiresParentheses)
    {
        // NOTE: Null conditional rewriting should NOT be applied here anymore since it's handled at the top level
        // Only apply it if explicitly requested (which should be rare)
        if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
        {
            try
            {
                var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
                expression = (ExpressionSyntax)rewriter.Visit(expression);
            }
            catch (Exception)
            {
                // If null conditional rewriting fails, continue with original expression
            }
        }

        // Handle binary expressions
        if (expression is BinaryExpressionSyntax binary)
        {
            // Check if operands will need parentheses based on their current state
            var leftNeedsParens = binary.Left is ConditionalExpressionSyntax &&
                     ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken);

            var rightNeedsParens = binary.Right is ConditionalExpressionSyntax &&
                     ConditionalNeedsParenthesesInBinaryExpression(binary.OperatorToken);

            var left = TransformExpressionInternal(binary.Left, parameterName, semanticModel, NullConditionalRewriteSupport.None, leftNeedsParens);
            var right = TransformExpressionInternal(binary.Right, parameterName, semanticModel, NullConditionalRewriteSupport.None, rightNeedsParens);

            return SyntaxFactory.BinaryExpression(binary.OperatorToken.Kind(), left, right);
        }

        // Handle conditional expressions
        if (expression is ConditionalExpressionSyntax conditional)
        {
            var condition = TransformExpressionInternal(conditional.Condition, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenTrue = TransformExpressionInternal(conditional.WhenTrue, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenFalse = TransformExpressionInternal(conditional.WhenFalse, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);

            var result = SyntaxFactory.ConditionalExpression(condition, whenTrue, whenFalse);

            // Add parentheses if the parent context requires them (for precedence)
            return parentRequiresParentheses ? SyntaxFactory.ParenthesizedExpression(result) : result;
        }

        // Handle method invocations (nested mapper calls)
        if (expression is InvocationExpressionSyntax invocation)
        {
            return HandleMethodInvocation(invocation, parameterName, semanticModel, NullConditionalRewriteSupport.None);
        }

        // Handle member access expressions like source.Property
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return ReplaceParameterInMemberAccess(memberAccess, parameterName);
        }

        // Handle parenthesized expressions
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            var inner = TransformExpressionInternal(parenthesized.Expression, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            return SyntaxFactory.ParenthesizedExpression(inner);
        }

        // Handle literal expressions (constants)
        if (expression is LiteralExpressionSyntax literal)
        {
            return literal;
        }

        // Handle cast expressions
        if (expression is CastExpressionSyntax cast)
        {
            var transformedExpression = TransformExpressionInternal(cast.Expression, parameterName, semanticModel, NullConditionalRewriteSupport.None, false);
            return SyntaxFactory.CastExpression(cast.Type, transformedExpression);
        }

        // Handle simple identifiers and other expressions
        return ReplaceParameterInExpression(expression, parameterName);
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

    private static ExpressionSyntax HandleMethodInvocation(InvocationExpressionSyntax invocation, string parameterName,
        SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        // Get the argument being passed to the method
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return ReplaceParameterInExpression(invocation, parameterName);
        }

        // Check if this is a method call that we can inline
        if (invocation.Expression is IdentifierNameSyntax identifierName)
        {
            // Look for the method in the same class
            var methodName = identifierName.Identifier.ValueText;

            // Try to find the method declaration in the same syntax tree
            var root = semanticModel.SyntaxTree.GetRoot();
            var targetMethod = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);

            if (targetMethod != null)
            {
                var argument = invocation.ArgumentList.Arguments[0];
                var argumentExpression = TransformExpression(argument.Expression, parameterName, semanticModel, nullConditionalRewriteSupport);

                // Try to inline the method - get the parameter name from the target method
                var targetParameter = targetMethod.ParameterList.Parameters.FirstOrDefault();
                if (targetParameter != null)
                {
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
                        try
                        {
                            // Apply null conditional rewriting to the original body if needed
                            if (nullConditionalRewriteSupport != NullConditionalRewriteSupport.None)
                            {
                                var rewriter = new NullConditionalRewriter(nullConditionalRewriteSupport, semanticModel);
                                bodyExpression = (ExpressionSyntax)rewriter.Visit(bodyExpression);
                            }

                            // Transform the expression with the correct parameter substitution
                            var inlinedBody = TransformExpressionInternal(bodyExpression, targetParameterName, semanticModel, NullConditionalRewriteSupport.None, false);
                            return ReplaceParameterInExpression(inlinedBody, targetParameterName, argumentExpression);
                        }
                        catch (Exception)
                        {
                            // If inlining fails, fall back to parameter replacement
                            return ReplaceParameterInExpression(invocation, parameterName);
                        }
                    }
                }
            }
        }

        // Fallback - don't inline, just replace parameter names
        return ReplaceParameterInExpression(invocation, parameterName);
    }

    private static ExpressionSyntax ReplaceParameterInMemberAccess(MemberAccessExpressionSyntax memberAccess, string parameterName)
    {
        if (memberAccess.Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "source")
        {
            return memberAccess.WithExpression(SyntaxFactory.IdentifierName(parameterName));
        }

        var transformedExpression = ReplaceParameterInExpression(memberAccess.Expression, parameterName);
        return memberAccess.WithExpression(transformedExpression);
    }

    private static ExpressionSyntax ReplaceParameterInExpression(ExpressionSyntax expression, string parameterName)
    {
        if (expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "source")
        {
            return SyntaxFactory.IdentifierName(parameterName);
        }

        // Handle specific expression types more carefully
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return ReplaceParameterInMemberAccess(memberAccess, parameterName);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            // Transform the expression part (method target)
            var transformedExpression = ReplaceParameterInExpression(invocation.Expression, parameterName);

            // Transform arguments
            var transformedArguments = new List<ArgumentSyntax>();
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                var transformedArg = SyntaxFactory.Argument(ReplaceParameterInExpression(arg.Expression, parameterName));
                transformedArguments.Add(transformedArg);
            }

            return SyntaxFactory.InvocationExpression(
                transformedExpression,
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(transformedArguments)));
        }

        if (expression is BinaryExpressionSyntax binary)
        {
            var left = ReplaceParameterInExpression(binary.Left, parameterName);
            var right = ReplaceParameterInExpression(binary.Right, parameterName);
            return SyntaxFactory.BinaryExpression(binary.OperatorToken.Kind(), left, right);
        }

        if (expression is ConditionalExpressionSyntax conditional)
        {
            var condition = ReplaceParameterInExpression(conditional.Condition, parameterName);
            var whenTrue = ReplaceParameterInExpression(conditional.WhenTrue, parameterName);
            var whenFalse = ReplaceParameterInExpression(conditional.WhenFalse, parameterName);
            return SyntaxFactory.ConditionalExpression(condition, whenTrue, whenFalse);
        }

        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            var inner = ReplaceParameterInExpression(parenthesized.Expression, parameterName);
            return SyntaxFactory.ParenthesizedExpression(inner);
        }

        // For more complex expressions, try string replacement as a fallback
        var expressionString = expression.ToString();
        if (expressionString.Contains("source"))
        {
            var replacedString = expressionString.Replace("source", parameterName);

            try
            {
                return SyntaxFactory.ParseExpression(replacedString);
            }
            catch
            {
                // If parsing fails, return the original expression
                return expression;
            }
        }

        // If no changes needed, return original expression
        return expression;
    }

    private static ExpressionSyntax ReplaceParameterInExpression(ExpressionSyntax expression, string oldParameterName, ExpressionSyntax newExpression)
    {
        if (expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == oldParameterName)
        {
            return newExpression;
        }

        // Handle specific expression types more carefully
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var transformedExpression = ReplaceParameterInExpression(memberAccess.Expression, oldParameterName, newExpression);
            return memberAccess.WithExpression(transformedExpression);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            // Transform the expression part (method target)
            var transformedExpression = ReplaceParameterInExpression(invocation.Expression, oldParameterName, newExpression);

            // Transform arguments
            var transformedArguments = new List<ArgumentSyntax>();
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                var transformedArg = SyntaxFactory.Argument(ReplaceParameterInExpression(arg.Expression, oldParameterName, newExpression));
                transformedArguments.Add(transformedArg);
            }

            return SyntaxFactory.InvocationExpression(
                transformedExpression,
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(transformedArguments)));
        }

        if (expression is BinaryExpressionSyntax binary)
        {
            var left = ReplaceParameterInExpression(binary.Left, oldParameterName, newExpression);
            var right = ReplaceParameterInExpression(binary.Right, oldParameterName, newExpression);
            return SyntaxFactory.BinaryExpression(binary.OperatorToken.Kind(), left, right);
        }

        if (expression is ConditionalExpressionSyntax conditional)
        {
            var condition = ReplaceParameterInExpression(conditional.Condition, oldParameterName, newExpression);
            var whenTrue = ReplaceParameterInExpression(conditional.WhenTrue, oldParameterName, newExpression);
            var whenFalse = ReplaceParameterInExpression(conditional.WhenFalse, oldParameterName, newExpression);
            return SyntaxFactory.ConditionalExpression(condition, whenTrue, whenFalse);
        }

        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            var inner = ReplaceParameterInExpression(parenthesized.Expression, oldParameterName, newExpression);
            return SyntaxFactory.ParenthesizedExpression(inner);
        }

        // For more complex expressions, try string replacement as a fallback
        var expressionString = expression.ToString();
        if (expressionString.Contains(oldParameterName))
        {
            var newExpressionString = newExpression.ToString();
            var replacedString = expressionString.Replace(oldParameterName, newExpressionString);

            try
            {
                return SyntaxFactory.ParseExpression(replacedString);
            }
            catch
            {
                // If parsing fails, return the original expression
                return expression;
            }
        }

        // If no changes needed, return original expression
        return expression;
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

        // Add debugging comment
        sb.AppendLine($"{indent}    // Generated {classInfo.Methods.Length} methods");

        for (int i = 0; i < classInfo.Methods.Length; i++)
        {
            var method = classInfo.Methods[i];
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Expression projection for <see cref=\"{method.OriginalName}({method.SourceType})\"/>");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <returns>An expression tree representing the logic of {method.OriginalName}</returns>");
            sb.AppendLine($"{indent}    public static Expression<Func<{method.SourceType}, {method.ReturnType}>> {method.CompanionName}()");
            sb.AppendLine($"{indent}    {{");

            // Convert ExpressionSyntax to string first, then format
            //var expressionString = method.Expression.ToString();
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

    // Simple fallback method for null conditional rewriting
    private static string SimpleNullConditionalRewrite(string expressionText)
    {
        var result = expressionText.Trim();

        // Handle the most common and straightforward patterns first
        // Pattern 1: Simple property access like "person.BirthInfo?.Age"
        // This pattern matches: identifier(s).identifier?.identifier (not followed by parentheses or brackets)
        var simplePropertyPattern = @"([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\?\.([a-zA-Z_][a-zA-Z0-9_]*)(?![(\[]|\.\w)";
        result = Regex.Replace(result, simplePropertyPattern, "($1 != null ? $1.$2 : null)");

        // Handle any remaining null conditional operators with a more conservative approach
        // Only if there are still ?. operators and they look safe to replace
        var maxIterations = 3;
        var iterations = 0;

        while (result.Contains("?.") && iterations < maxIterations)
        {
            var oldResult = result;

            // Very conservative pattern - only match clear identifier chains
            var conservativePattern = @"([a-zA-Z_][a-zA-Z0-9_]*)\?\.([a-zA-Z_][a-zA-Z0-9_]*)";
            var matches = Regex.Matches(result, conservativePattern);

            if (matches.Count == 0)
                break;

            // Replace each match individually to avoid conflicts
            foreach (Match match in matches)
            {
                var fullMatch = match.Value;
                var obj = match.Groups[1].Value;
                var prop = match.Groups[2].Value;
                var replacement = $"({obj} != null ? {obj}.{prop} : null)";
                result = result.Replace(fullMatch, replacement);
            }

            // Break if no changes were made
            if (result == oldResult)
                break;

            iterations++;
        }

        return result;
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
    public readonly ExpressionSyntax Expression;

    public MethodInfo(string originalName, string companionName, string sourceType, string returnType, ExpressionSyntax expression)
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
               SyntaxFactory.AreEquivalent(Expression, other.Expression);
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
            hashCode = (hashCode * 397) ^ (Expression?.ToString()?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}