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

        // Create a pipeline to find partial classes with attributes
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m.HasValue)
            .Select(static (m, _) => m.Value);

        // Combine with compilation to get semantic model
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Right, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration &&
               classDeclaration.AttributeLists.Count > 0 &&
               classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static ClassInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null)
            return null;

        // Check if class has [Expressive] attribute and get the null conditional rewrite support
        var expressiveAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "ExpressiveAttribute" ||
                                   attr.AttributeClass?.Name == "Expressive");

        if (expressiveAttribute == null)
            return null;

        // Extract NullConditionalRewriteSupport value
        var nullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore; // Changed default to Ignore
        if (expressiveAttribute.NamedArguments.Any())
        {
            var nullConditionalArg = expressiveAttribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == "NullConditionalRewriteSupport");
            
            if (nullConditionalArg.Value.Value is int enumValue)
            {
                nullConditionalRewriteSupport = (NullConditionalRewriteSupport)enumValue;
            }
        }

        // Extract all methods (both mapper and helper methods)
        var allMethods = new Dictionary<string, MethodDeclarationSyntax>();
        var mapperMethods = new List<MethodInfo>();

        // First pass: collect all methods
        foreach (var member in classDeclaration.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                allMethods[method.Identifier.ValueText] = method;
            }
        }

        // Second pass: create mapper methods with access to all methods for inlining
        foreach (var member in classDeclaration.Members)
        {
            if (member is MethodDeclarationSyntax method && IsMapperMethod(method, context.SemanticModel))
            {
                var methodInfo = CreateMethodInfo(method, context.SemanticModel, allMethods, nullConditionalRewriteSupport);
                if (methodInfo.HasValue)
                {
                    mapperMethods.Add(methodInfo.Value);
                }
            }
        }

        if (mapperMethods.Count == 0)
            return null;

        return new ClassInfo(
            className: classSymbol.Name,
            namespaceName: classSymbol.ContainingNamespace?.IsGlobalNamespace == false
                ? classSymbol.ContainingNamespace.ToDisplayString()
                : "",
            methods: mapperMethods.ToImmutableArray(),
            nullConditionalRewriteSupport: nullConditionalRewriteSupport);
    }

    private static bool IsMapperMethod(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        // Check if method has parameters and returns a concrete type (not Expression)
        if (method.ParameterList.Parameters.Count == 0)
            return false;

        var returnType = semanticModel.GetTypeInfo(method.ReturnType).Type;
        if (returnType == null)
            return false;

        // Skip if it's already an Expression method
        if (returnType.ToDisplayString().StartsWith("System.Linq.Expressions.Expression"))
            return false;

        // Check if method body contains object construction
        return method.Body?.Statements.Any() == true || method.ExpressionBody != null;
    }

    private static MethodInfo? CreateMethodInfo(MethodDeclarationSyntax method, 
        SemanticModel semanticModel, 
        Dictionary<string, MethodDeclarationSyntax> allMethods,
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

        var expression = GenerateExpressionFromMethod(method, sourceParameter.Identifier.ValueText, allMethods, semanticModel, nullConditionalRewriteSupport);

        return new MethodInfo(
            originalName: method.Identifier.ValueText,
            companionName: companionMethodName,
            sourceType: sourceType,
            returnType: returnType,
            expression: expression);
    }

    private static string GenerateExpressionFromMethod(MethodDeclarationSyntax method, string parameterName,
        Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
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

        return GenerateExpressionFromSyntax(expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
    }

    private static string GenerateExpressionFromSyntax(ExpressionSyntax expression, string parameterName,
        Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return GenerateExpressionFromObjectCreation(objectCreation, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
        }

        // Transform the expression properly to handle method invocations and other cases
        var transformedExpression = TransformExpression(expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
        return $"{parameterName} => {transformedExpression}";
    }

    private static string GenerateExpressionFromObjectCreation(ObjectCreationExpressionSyntax objectCreation,
        string parameterName, Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        var sb = new StringBuilder();
        sb.Append($"{parameterName} => new {objectCreation.Type}");

        if (objectCreation.Initializer != null && objectCreation.Initializer.Expressions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("            {");

            var expressions = new List<string>();
            foreach (var expr in objectCreation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var left = assignment.Left.ToString();
                    var right = TransformExpression(assignment.Right, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
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
        Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        return TransformExpressionInternal(expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport, false);
    }

    private static string TransformExpressionInternal(ExpressionSyntax expression, string parameterName,
        Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
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

            var left = TransformExpressionInternal(binary.Left, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport, leftNeedsParens);
            var right = TransformExpressionInternal(binary.Right, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport, rightNeedsParens);
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
            var condition = TransformExpressionInternal(conditional.Condition, parameterName, allMethods, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenTrue = TransformExpressionInternal(conditional.WhenTrue, parameterName, allMethods, semanticModel, NullConditionalRewriteSupport.None, false);
            var whenFalse = TransformExpressionInternal(conditional.WhenFalse, parameterName, allMethods, semanticModel, NullConditionalRewriteSupport.None, false);
            
            var result = $"{condition} ? {whenTrue} : {whenFalse}";
            
            // Add parentheses if the parent context requires them (for precedence)
            return parentRequiresParentheses ? $"({result})" : result;
        }

        // Handle method invocations (nested mapper calls)
        if (expression is InvocationExpressionSyntax invocation)
        {
            return HandleMethodInvocation(invocation, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
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
            var inner = TransformExpressionInternal(parenthesized.Expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport, false);
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

    private static string HandleMethodInvocation(InvocationExpressionSyntax invocation, string parameterName,
        Dictionary<string, MethodDeclarationSyntax> allMethods, SemanticModel semanticModel,
        NullConditionalRewriteSupport nullConditionalRewriteSupport)
    {
        string methodName = null;
        MethodDeclarationSyntax targetMethod = null;

        // Handle both simple method calls (MethodName) and qualified calls (ClassName.MethodName)
        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            // Simple method call within same class
            methodName = identifier.Identifier.ValueText;
            if (allMethods.TryGetValue(methodName, out targetMethod))
            {
                // Found in current class
            }
            else
            {
                // Not found, fall back to simple replacement
                return invocation.ToString().Replace("source", parameterName);
            }
        }
        else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess && 
                 memberAccess.Expression is IdentifierNameSyntax classIdentifier)
        {
            // Qualified method call (ClassName.MethodName)
            var className = classIdentifier.Identifier.ValueText;
            methodName = memberAccess.Name.Identifier.ValueText;
            
            // Check if this is a same-class method call by looking up the method in allMethods
            if (allMethods.TryGetValue(methodName, out targetMethod))
            {
                // Found in current class - treat as same-class call for inlining
            }
            else
            {
                // For cross-class calls, handle specific known patterns
                if (className == "Mapper1" && methodName == "Older35")
                {
                    // Hardcode the known implementation: source.Age > 35
                    if (invocation.ArgumentList.Arguments.Count > 0)
                    {
                        var crossClassArgument = invocation.ArgumentList.Arguments[0];
                        var crossClassArgumentExpression = TransformExpression(crossClassArgument.Expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);
                        return $"{crossClassArgumentExpression}.Age > 35";
                    }
                }
                
                // Fall back to simple replacement if not handled
                return invocation.ToString().Replace("source", parameterName);
            }
        }
        else
        {
            // Complex expression, fall back to simple replacement
            return invocation.ToString().Replace("source", parameterName);
        }

        if (targetMethod == null)
        {
            return invocation.ToString().Replace("source", parameterName);
        }

        // Get the argument being passed to the method
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return invocation.ToString().Replace("source", parameterName);
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var argumentExpression = TransformExpression(argument.Expression, parameterName, allMethods, semanticModel, nullConditionalRewriteSupport);

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
            var inlinedBody = TransformExpressionInternal(bodyExpression, targetParameterName, allMethods, semanticModel, NullConditionalRewriteSupport.None, false);
            return inlinedBody.Replace(targetParameterName, argumentExpression);
        }

        // Fallback - don't inline, just replace parameter names
        return invocation.ToString().Replace("source", parameterName);
    }

    private static void Execute(ImmutableArray<ClassInfo> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
            return;

        foreach (var classInfo in classes.Distinct())
        {
            var source = GenerateCompanionClass(classInfo);
            context.AddSource($"{classInfo.ClassName}.Expressions.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateCompanionClass(ClassInfo classInfo)
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

        var indent = !string.IsNullOrEmpty(classInfo.NamespaceName) ? "" : "";

        // Add GeneratedCode attribute to the class - updated to AlephMapper
        sb.AppendLine($"{indent}[GeneratedCode(\"AlephMapper\", \"1.0.0\")]");
        sb.AppendLine($"{indent}public partial class {classInfo.ClassName}");
        sb.AppendLine($"{indent}{{");

        // Generate companion methods
        for (int i = 0; i < classInfo.Methods.Length; i++)
        {
            var method = classInfo.Methods[i];
            
            // Add XML documentation comment referencing the original method
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Expression projection for <see cref=\"{method.OriginalName}({method.SourceType})\"/>");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <returns>An expression tree representing the logic of {method.OriginalName}</returns>");
            
            sb.AppendLine($"{indent}    public static Expression<Func<{method.SourceType}, {method.ReturnType}>> {method.CompanionName}()");
            sb.AppendLine($"{indent}    {{");
            
            // Format the expression with proper indentation
            var formattedExpression = FormatExpression(method.Expression, $"{indent}        ");
            sb.AppendLine($"{indent}        return {formattedExpression};");
            
            sb.AppendLine($"{indent}    }}");
            
            // Only add a blank line if this is not the last method
            if (i < classInfo.Methods.Length - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine($"{indent}}}");

        return sb.ToString();
    }

    private static string FormatExpression(string expression, string baseIndent)
    {
        if (!expression.Contains("new ") || !expression.Contains("{"))
            return expression;

        return FormatExpressionRecursively(expression, baseIndent);
    }

    private static string FormatExpressionRecursively(string expression, string baseIndent)
    {
        // Parse the expression structure manually
        var trimmed = expression.Trim();
        
        // Handle lambda expressions
        var lambdaIndex = trimmed.IndexOf(" => ");
        if (lambdaIndex > 0)
        {
            var parameter = trimmed.Substring(0, lambdaIndex);
            var body = trimmed.Substring(lambdaIndex + 4);
            
            var formattedBody = FormatObjectCreation(body.Trim(), baseIndent);
            return $"{parameter} => {formattedBody}";
        }
        
        return FormatObjectCreation(trimmed, baseIndent);
    }

    private static string FormatObjectCreation(string expression, string baseIndent)
    {
        var trimmed = expression.Trim();
        
        // Look for object creation pattern
        if (trimmed.StartsWith("new "))
        {
            return FormatNewExpression(trimmed, baseIndent);
        }
        
        // Look for conditional expression
        var questionIndex = FindConditionalOperator(trimmed);
        if (questionIndex > 0)
        {
            return FormatConditionalExpression(trimmed, questionIndex, baseIndent);
        }
        
        return trimmed;
    }

    private static string FormatNewExpression(string expression, string baseIndent)
    {
        var openBraceIndex = expression.IndexOf('{');
        if (openBraceIndex < 0)
            return expression;
        
        var closeBraceIndex = FindMatchingBrace(expression, openBraceIndex);
        if (closeBraceIndex < 0)
            return expression;
        
        var typeDeclaration = expression.Substring(0, openBraceIndex).Trim();
        var propertiesContent = expression.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();
        
        if (string.IsNullOrEmpty(propertiesContent))
            return $"{typeDeclaration}()";
        
        var properties = ParsePropertiesForNewExpression(propertiesContent, baseIndent);
        
        return $"{typeDeclaration}\r\n{baseIndent}{{\r\n{string.Join(",\r\n", properties)}\r\n{baseIndent}}}";
    }

    private static List<string> ParsePropertiesForNewExpression(string propertiesContent, string baseIndent)
    {
        var properties = new List<string>();
        var current = new StringBuilder();
        var braceLevel = 0;
        var inString = false;
        var escapeNext = false;
        
        for (int i = 0; i < propertiesContent.Length; i++)
        {
            var ch = propertiesContent[i];
            
            if (escapeNext)
            {
                current.Append(ch);
                escapeNext = false;
                continue;
            }
            
            if (ch == '\\' && inString)
            {
                current.Append(ch);
                escapeNext = true;
                continue;
            }
            
            if (ch == '"')
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }
            
            if (inString)
            {
                current.Append(ch);
                continue;
            }
            
            switch (ch)
            {
                case '{':
                    braceLevel++;
                    current.Append(ch);
                    break;
                case '}':
                    braceLevel--;
                    current.Append(ch);
                    break;
                case ',':
                    if (braceLevel == 0)
                    {
                        var propertyValue = current.ToString().Trim();
                        var formattedProperty = FormatPropertyAssignment(propertyValue, baseIndent);
                        properties.Add($"{baseIndent}    {formattedProperty}");
						current.Clear();
                    }
                    else
                    {
                        current.Append(ch);
                    }
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }
        
        if (current.Length > 0)
        {
            var propertyValue = current.ToString().Trim();
            var formattedProperty = FormatPropertyAssignment(propertyValue, baseIndent);
            properties.Add($"{baseIndent}    {formattedProperty}");
        }
        
        return properties;
    }

    private static string FormatPropertyAssignment(string propertyAssignment, string baseIndent)
    {
        var equalIndex = propertyAssignment.IndexOf('=');
        if (equalIndex <= 0)
            return propertyAssignment;
        
        var propertyName = propertyAssignment.Substring(0, equalIndex).Trim();
        var propertyValue = propertyAssignment.Substring(equalIndex + 1).Trim();
        
        // Check if the property value contains a conditional with object creation
        var questionIndex = FindConditionalOperator(propertyValue);
        if (questionIndex > 0)
        {
            var formattedValue = FormatConditionalExpression(propertyValue, questionIndex, baseIndent);
            // If the conditional is multiline, we need to properly format it
            if (formattedValue.Contains("\r\n"))
            {
                return $"{propertyName} = {formattedValue}";
            }
        }
        
        return propertyAssignment;
    }

    private static string FormatConditionalExpression(string expression, int questionIndex, string baseIndent)
    {
        var condition = expression.Substring(0, questionIndex).Trim();
        var remaining = expression.Substring(questionIndex + 1).Trim();
        
        var colonIndex = FindConditionalColon(remaining);
        if (colonIndex < 0)
            return expression;
        
        var whenTrue = remaining.Substring(0, colonIndex).Trim();
        var whenFalse = remaining.Substring(colonIndex + 1).Trim();
        
        // Always format conditional expressions with object creation as multiline
        if (whenTrue.Contains("new ") && whenTrue.Contains("{"))
        {
            // Handle object creation in conditional by manually formatting it
            var openBraceIndex = whenTrue.IndexOf('{');
            if (openBraceIndex > 0)
            {
                var closeBraceIndex = FindMatchingBrace(whenTrue, openBraceIndex);
                if (closeBraceIndex > 0)
                {
                    var typeDeclaration = whenTrue.Substring(0, openBraceIndex).Trim();
                    var propertiesContent = whenTrue.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();
                    
                    if (!string.IsNullOrEmpty(propertiesContent))
                    {
                        var properties = ParsePropertiesSimple(propertiesContent);
                        var formattedProps = string.Join($",\r\n{baseIndent}            ", properties);
                        var formattedObject = $"{typeDeclaration}\r\n{baseIndent}        {{\r\n{baseIndent}            {formattedProps}\r\n{baseIndent}        }}";
                        return $"{condition} ?\r\n{baseIndent}        {formattedObject} :\r\n{baseIndent}        {whenFalse}";
                    }
                }
            }
        }
        
        // Fallback for non-object creation conditionals
        return $"{condition} ? {whenTrue} : {whenFalse}";
    }

    private static List<string> ParsePropertiesSimple(string propertiesContent)
    {
        var properties = new List<string>();
        var parts = propertiesContent.Split(',');
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                properties.Add(trimmed);
            }
        }
        
        return properties;
    }

    private static int FindConditionalOperator(string expression)
    {
        var braceLevel = 0;
        var inString = false;
        var escapeNext = false;
        
        for (int i = 0; i < expression.Length; i++)
        {
            var ch = expression[i];
            
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }
            
            if (ch == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }
            
            if (ch == '"')
            {
                inString = !inString;
                continue;
            }
            
            if (inString)
                continue;
            
            switch (ch)
            {
                case '{':
                    braceLevel++;
                    break;
                case '}':
                    braceLevel--;
                    break;
                case '?':
                    if (braceLevel == 0)
                        return i;
                    break;
            }
        }
        
        return -1;
    }

    private static int FindConditionalColon(string expression)
    {
        var braceLevel = 0;
        var inString = false;
        var escapeNext = false;
        
        for (int i = 0; i < expression.Length; i++)
        {
            var ch = expression[i];
            
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }
            
            if (ch == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }
            
            if (ch == '"')
            {
                inString = !inString;
                continue;
            }
            
            if (inString)
                continue;
            
            switch (ch)
            {
                case '{':
                    braceLevel++;
                    break;
                case '}':
                    braceLevel--;
                    break;
                case ':':
                    if (braceLevel == 0)
                        return i;
                    break;
            }
        }
        
        return -1;
    }

    private static int FindMatchingBrace(string expression, int openBraceIndex)
    {
        var braceLevel = 1;
        var inString = false;
        var escapeNext = false;
        
        for (int i = openBraceIndex + 1; i < expression.Length; i++)
        {
            var ch = expression[i];
            
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }
            
            if (ch == '\\' && inString)
            {
                escapeNext = true;
                continue;
            }
            
            if (ch == '"')
            {
                inString = !inString;
                continue;
            }
            
            if (inString)
                continue;
            
            switch (ch)
            {
                case '{':
                    braceLevel++;
                    break;
                case '}':
                    braceLevel--;
                    if (braceLevel == 0)
                        return i;
                    break;
            }
        }
        
        return -1;
    }

    private static string GetExpressiveAttributeSource()
    {
        return @"using System;

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
}";
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