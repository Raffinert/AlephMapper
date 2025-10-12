using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper;

//internal sealed class UpdateInliningResolver(SemanticModel model, IDictionary<IMethodSymbol, MappingModel> catalog)
//{
//    private readonly InliningResolver _inliningResolver = new(model, catalog);

//    public SyntaxNode Visit(SyntaxNode node)
//    {
//        // For updateable methods, we use the same inlining logic as expressions
//        // The difference is in how we process the inlined result in EmitHelpers
//        return _inliningResolver.Visit(node);
//    }
//}

internal sealed class UpdateableExpressionProcessor(string destPrefix, UpdateableTypeContext typeContext)
{
    private readonly List<string> _lines = [];

    public List<string> ProcessObjectCreation(ObjectCreationExpressionSyntax objectCreation)
    {
        _lines.Clear();

        if (objectCreation?.Initializer?.Expressions == null) return [];
        foreach (var expr in objectCreation.Initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assignment)
            {
                ProcessAssignment(assignment, destPrefix);
            }
        }

        return _lines.ToList();
    }

    private void ProcessAssignment(AssignmentExpressionSyntax assignment, string currentDestPath)
    {
        var propertyName = assignment.Left.ToString();
        var rightExpression = assignment.Right;
        var fullDestPath = $"{currentDestPath}.{propertyName}";

        ProcessExpression(rightExpression, fullDestPath);
    }

    private void ProcessExpression(ExpressionSyntax expression, string fullDestPath)
    {
        switch (expression)
        {
            case ConditionalExpressionSyntax conditional:
                ProcessConditionalExpression(conditional, fullDestPath);
                break;

            case ObjectCreationExpressionSyntax objectCreation:
                ProcessDirectObjectCreation(objectCreation, fullDestPath);
                break;

            default:
                // Simple property assignment
                _lines.Add($"{fullDestPath} = {expression};");
                break;
        }
    }

    private void ProcessConditionalExpression(ConditionalExpressionSyntax conditional, string fullDestPath)
    {
        var conditionText = conditional.Condition.ToString();
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        var isTrueNull = IsNullExpression(whenTrue);
        var isFalseNull = IsNullExpression(whenFalse);

        if (!isTrueNull && isFalseNull)
        {
            // Pattern: condition ? object_creation : null
            // Source check: if condition is true, assign object; if false, assign null
            ProcessConditionalWithObjectCreation(conditionText, whenTrue, fullDestPath);
        }
        else if (isTrueNull && !isFalseNull)
        {
            // Pattern: condition ? null : object_creation
            // Source check: if condition is true, assign null; if false, assign object  
            ProcessConditionalWithObjectCreation($"!({conditionText})", whenFalse, fullDestPath);
        }
        else
        {
            // Both sides non-null or both null - direct assignment
            _lines.Add($"{fullDestPath} = {conditional};");
        }
    }

    private void ProcessConditionalWithObjectCreation(string sourceCondition, ExpressionSyntax objectExpression, string fullDestPath)
    {
        // This method handles the correct separation of source null checking from target object management

        if (objectExpression is ObjectCreationExpressionSyntax objectCreation)
        {
            // Generate the logic:
            // if (source condition is met for object creation)
            //   ensure target object exists (create if null, reuse if exists)  
            //   update all properties of target object
            // else
            //   set target to null (only if target can be null)

            _lines.Add($"if ({sourceCondition})");
            _lines.Add("{");

            // Ensure target object exists - this is independent of source condition
            // Only generate null check if the target property can actually be null
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                _lines.Add($"    if ({fullDestPath} == null)");
                _lines.Add($"        {fullDestPath} = new {objectCreation.Type}();");
            }
            else
            {
                // For value types, we don't need a null check - just assign directly
                // This handles cases where the target is a value type that can't be null
                _lines.Add($"    {fullDestPath} = new {objectCreation.Type}();");
            }

            // Now update all properties of the target object
            if (objectCreation.Initializer?.Expressions != null)
            {
                foreach (var expr in objectCreation.Initializer.Expressions)
                {
                    if (expr is AssignmentExpressionSyntax nestedAssignment)
                    {
                        var nestedPropertyName = nestedAssignment.Left.ToString();
                        var nestedDestPath = $"{fullDestPath}.{nestedPropertyName}";

                        // Add indentation for the nested processing
                        var nestedLines = new List<string>();

                        // Process the nested assignment
                        ProcessNestedExpression(nestedAssignment.Right, nestedDestPath, nestedLines, "    ");

                        _lines.AddRange(nestedLines);
                    }
                }
            }

            _lines.Add("}");
            
            // Only add else clause to set target to null if the target can be null
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                _lines.Add("else");
                _lines.Add("{");
                _lines.Add($"    {fullDestPath} = null;");
                _lines.Add("}");
            }
        }
        else
        {
            // Simple conditional assignment
            _lines.Add($"if ({sourceCondition})");
            _lines.Add("{");
            _lines.Add($"    {fullDestPath} = {objectExpression};");
            _lines.Add("}");
            
            // Only add else clause if the target can be null
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                _lines.Add("else");
                _lines.Add("{");
                _lines.Add($"    {fullDestPath} = null;");
                _lines.Add("}");
            }
        }
    }

    private void ProcessNestedExpression(ExpressionSyntax expression, string fullDestPath, List<string> lines, string indent)
    {
        switch (expression)
        {
            case ConditionalExpressionSyntax conditional:
                ProcessNestedConditional(conditional, fullDestPath, lines, indent);
                break;

            case ObjectCreationExpressionSyntax objectCreation:
                ProcessNestedObjectCreation(objectCreation, fullDestPath, lines, indent);
                break;

            default:
                // Simple property assignment
                lines.Add($"{indent}{fullDestPath} = {expression};");
                break;
        }
    }

    private void ProcessNestedConditional(ConditionalExpressionSyntax conditional, string fullDestPath, List<string> lines, string indent)
    {
        var conditionText = conditional.Condition.ToString();
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        var isTrueNull = IsNullExpression(whenTrue);
        var isFalseNull = IsNullExpression(whenFalse);

        if (!isTrueNull && isFalseNull)
        {
            // Pattern: condition ? object_creation : null
            ProcessNestedConditionalWithObject(conditionText, whenTrue, fullDestPath, lines, indent);
        }
        else if (isTrueNull && !isFalseNull)
        {
            // Pattern: condition ? null : object_creation
            ProcessNestedConditionalWithObject($"!({conditionText})", whenFalse, fullDestPath, lines, indent);
        }
        else
        {
            // Direct assignment
            lines.Add($"{indent}{fullDestPath} = {conditional};");
        }
    }

    private void ProcessNestedConditionalWithObject(string sourceCondition, ExpressionSyntax objectExpression, string fullDestPath, List<string> lines, string indent)
    {
        if (objectExpression is ObjectCreationExpressionSyntax objectCreation)
        {
            lines.Add($"{indent}if ({sourceCondition})");
            lines.Add($"{indent}{{");

            // Ensure nested target object exists - only add null check if target can be null
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                lines.Add($"{indent}    if ({fullDestPath} == null)");
                lines.Add($"{indent}        {fullDestPath} = new {objectCreation.Type}();");
            }
            else
            {
                // For value types, just assign directly
                lines.Add($"{indent}    {fullDestPath} = new {objectCreation.Type}();");
            }

            // Process nested properties
            if (objectCreation.Initializer?.Expressions != null)
            {
                foreach (var expr in objectCreation.Initializer.Expressions)
                {
                    if (expr is AssignmentExpressionSyntax nestedAssignment)
                    {
                        var nestedPropertyName = nestedAssignment.Left.ToString();
                        var nestedDestPath = $"{fullDestPath}.{nestedPropertyName}";

                        ProcessNestedExpression(nestedAssignment.Right, nestedDestPath, lines, indent + "    ");
                    }
                }
            }

            lines.Add($"{indent}}}");
            
            // Only add else clause if target can be null
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                lines.Add($"{indent}else");
                lines.Add($"{indent}{{");
                lines.Add($"{indent}    {fullDestPath} = null;");
                lines.Add($"{indent}}}");
            }
        }
        else
        {
            lines.Add($"{indent}if ({sourceCondition})");
            lines.Add($"{indent}{{");
            lines.Add($"{indent}    {fullDestPath} = {objectExpression};");
            lines.Add($"{indent}}}");
            
            // Only add else clause if target can be null 
            if (typeContext.CanPropertyBeNull(fullDestPath))
            {
                lines.Add($"{indent}else");
                lines.Add($"{indent}{{");
                lines.Add($"{indent}    {fullDestPath} = null;");
                lines.Add($"{indent}}}");
            }
        }
    }

    private void ProcessNestedObjectCreation(ObjectCreationExpressionSyntax objectCreation, string fullDestPath, List<string> lines, string indent)
    {
        // Direct object creation - ensure target exists and update properties
        if (typeContext.CanPropertyBeNull(fullDestPath))
        {
            lines.Add($"{indent}if ({fullDestPath} == null)");
            lines.Add($"{indent}    {fullDestPath} = new {objectCreation.Type}();");
        }
        else
        {
            // For value types, just assign directly
            lines.Add($"{indent}{fullDestPath} = new {objectCreation.Type}();");
        }

        if (objectCreation.Initializer?.Expressions != null)
        {
            foreach (var expr in objectCreation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax nestedAssignment)
                {
                    var nestedPropertyName = nestedAssignment.Left.ToString();
                    var nestedDestPath = $"{fullDestPath}.{nestedPropertyName}";
                    ProcessNestedExpression(nestedAssignment.Right, nestedDestPath, lines, indent);
                }
            }
        }
    }

    private void ProcessDirectObjectCreation(ObjectCreationExpressionSyntax objectCreation, string fullDestPath)
    {
        // Direct object creation - ensure target exists and update properties
        if (typeContext.CanPropertyBeNull(fullDestPath))
        {
            _lines.Add($"if ({fullDestPath} == null)");
            _lines.Add($"    {fullDestPath} = new {objectCreation.Type}();");
        }
        else
        {
            // For value types, just assign directly
            _lines.Add($"{fullDestPath} = new {objectCreation.Type}();");
        }

        if (objectCreation.Initializer?.Expressions != null)
        {
            foreach (var expr in objectCreation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax nestedAssignment)
                {
                    var nestedPropertyName = nestedAssignment.Left.ToString();
                    var nestedDestPath = $"{fullDestPath}.{nestedPropertyName}";
                    ProcessExpression(nestedAssignment.Right, nestedDestPath);
                }
            }
        }
    }

    private static bool IsNullExpression(ExpressionSyntax expression)
    {
        return expression?.ToString().Trim() == "null";
    }
}

internal static class EmitHelpers
{
    public static bool TryBuildUpdateAssignmentsWithInlining(ExpressionSyntax inlinedBody, string destPrefix, List<string> lines, SemanticModel semanticModel = null)
    {
        if (inlinedBody is not ObjectCreationExpressionSyntax oce)
            return false;

        if (oce.Initializer?.Expressions == null || oce.Initializer.Expressions.Count == 0)
            return false;

        // Collect type information from the syntax tree
        var typeContext = semanticModel != null 
            ? TypeAnnotationCollector.CollectTypeInformation(inlinedBody, semanticModel, destPrefix)
            : new UpdateableTypeContext(); // Fallback to empty context for backward compatibility

        var processor = new UpdateableExpressionProcessor(destPrefix, typeContext);
        var processedLines = processor.ProcessObjectCreation(oce);

        lines.AddRange(processedLines);
        return lines.Count > 0;
    }
}