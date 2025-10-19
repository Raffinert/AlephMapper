using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper;

internal sealed class UpdateableExpressionProcessor(string destPrefix, PropertyMappingContext typeContext)
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

    //public List<string> ProcessRootConditionalExpression(ConditionalExpressionSyntax conditional, string currentDestPath)
    //{
    //    _lines.Clear();
    //    ProcessConditionalExpression(conditional, currentDestPath);
    //    return _lines.ToList();
    //}

    private void ProcessAssignment(AssignmentExpressionSyntax assignment, string currentDestPath)
    {
        var propertyName = assignment.Left.ToString();
        var rightExpression = assignment.Right;
        var fullDestPath = $"{currentDestPath}.{propertyName}";

        ProcessExpression(rightExpression, fullDestPath);
    }

    private void ProcessExpression(ExpressionSyntax expression, string fullDestPath)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            _lines.Add($"// Skipping collection property: {fullDestPath}");
            _lines.Add($"// Collection properties are not updated in updateable methods for safety");
            return;
        }

        switch (expression)
        {
            case ConditionalExpressionSyntax conditional:
                ProcessConditionalExpression(conditional, fullDestPath);
                break;

            case ObjectCreationExpressionSyntax objectCreation:
                ProcessDirectObjectCreation(objectCreation, fullDestPath);
                break;

            default:
                // Simple property assignment - check for value type issues
                if (IsValueTypePropertyAssignment(fullDestPath))
                {
                    // For value types, we can't do property-by-property assignment
                    _lines.Add($"// Warning: Cannot assign to property of value type: {fullDestPath} = {expression};");
                    _lines.Add($"// Consider restructuring to avoid nested value type property assignments");
                }
                else
                {
                    _lines.Add($"{fullDestPath} = {expression};");
                }
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

    internal List<string> ProcessRootConditionalExpression(ConditionalExpressionSyntax conditional, string currentDestPath)
    {
        _lines.Clear();
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        var isTrueNull = IsNullExpression(whenTrue);
        var isFalseNull = IsNullExpression(whenFalse);

        if (!isTrueNull && isFalseNull)
        {
            ProcessExpression(whenTrue, currentDestPath);
        }
        else if (isTrueNull && !isFalseNull)
        {
            ProcessExpression(whenFalse, currentDestPath);
        }
        else
        {
            // Both sides non-null or both null - direct assignment
            _lines.Add($"{currentDestPath} = {conditional};");
        }
        return _lines;
    }

    private void ProcessConditionalWithObjectCreation(string sourceCondition, ExpressionSyntax objectExpression, string fullDestPath)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            _lines.Add($"// Skipping collection property: {fullDestPath}");
            _lines.Add($"// Collection properties are not updated in updateable methods for safety");
            return;
        }

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
            _lines.Add($"    {fullDestPath} = {objectExpression.WithoutTrivia()};");
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
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
            lines.Add($"{indent}// Collection properties are not updated in updateable methods for safety");
            return;
        }

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
                lines.Add($"{indent}{fullDestPath} = {expression.WithoutTrivia()};");
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
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
            lines.Add($"{indent}// Collection properties are not updated in updateable methods for safety");
            return;
        }

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
            lines.Add($"{indent}    {fullDestPath} = {objectExpression.WithoutTrivia()};");
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
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
            lines.Add($"{indent}// Collection properties are not updated in updateable methods for safety");
            return;
        }

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
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            _lines.Add($"// Skipping collection property: {fullDestPath}");
            _lines.Add($"// Collection properties are not updated in updateable methods for safety");
            return;
        }

        // Check if we're trying to assign to a property of a value type
        // For example: dest.SomeStruct.Property = value
        // This won't work because SomeStruct returns a copy
        if (IsValueTypePropertyAssignment(fullDestPath))
        {
            // For value types, we can't do property-by-property assignment
            // Instead, we need to reconstruct the entire path
            HandleValueTypeAssignment(objectCreation, fullDestPath);
            return;
        }

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

    private bool IsValueTypePropertyAssignment(string fullDestPath)
    {
        // Check if this is an assignment to a property of a value type
        // e.g., "dest.SomeStruct.Property" where SomeStruct is a value type
        var pathParts = fullDestPath.Split('.');
        if (pathParts.Length <= 2) return false; // dest.Property is fine

        // Check each intermediate path to see if it's a value type
        for (int i = 1; i < pathParts.Length - 1; i++)
        {
            var intermediatePath = string.Join(".", pathParts.Take(i + 1));
            if (typeContext.IsValueType(intermediatePath) && !typeContext.IsNullableValueType(intermediatePath))
            {
                return true; // Found a non-nullable value type in the path
            }
        }

        return false;
    }

    private void HandleValueTypeAssignment(ObjectCreationExpressionSyntax objectCreation, string fullDestPath)
    {
        // For value type assignments, we need to construct the entire path
        // This is a complex case that requires reconstructing parent structs

        // For now, let's just do a direct assignment to the full path
        // This will work for simple cases but may fail for deeply nested value types
        _lines.Add($"{fullDestPath} = {objectCreation.WithoutTrivia()};");

        // Note: This is a simplified approach. A full solution would need to:
        // 1. Identify the value type property in the path
        // 2. Reconstruct that struct with the new nested value
        // 3. Assign the reconstructed struct back to its parent
        // This is quite complex and may not be worth the effort for edge cases.
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
        // Collect type information from the syntax tree
        var typeContext = semanticModel != null
            ? PropertyTypeInfoCollector.CollectTypeInformation(inlinedBody, semanticModel, destPrefix)
            : new PropertyMappingContext(); // Fallback to empty context for backward compatibility

        var processor = new UpdateableExpressionProcessor(destPrefix, typeContext);
        List<string> processedLines;

        switch (inlinedBody)
        {
            case ObjectCreationExpressionSyntax oce:
                if (oce.Initializer?.Expressions == null || oce.Initializer.Expressions.Count == 0)
                    return false;
                
                processedLines = processor.ProcessObjectCreation(oce);
                break;

            case ConditionalExpressionSyntax conditional:
                // Handle conditional expressions like: condition ? new Type { ... } : null
                // or: condition ? null : new Type { ... }
                processedLines = processor.ProcessRootConditionalExpression(conditional, destPrefix);
                break;

            default:
                return false;
        }

        lines.AddRange(processedLines);
        return lines.Count > 0;
    }
}