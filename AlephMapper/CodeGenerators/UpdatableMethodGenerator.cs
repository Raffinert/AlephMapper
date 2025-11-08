using System.Collections.Generic;
using System.Linq;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.CodeGenerators;

internal sealed class UpdatableMethodGenerator(string destPrefix, PropertyMappingContext typeContext, string sourceParamName)
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
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            _lines.Add($"// Skipping collection property: {fullDestPath}");
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
                    _lines.Add($"// Warning: Cannot assign to property of value type: {fullDestPath} = {NormalizeConditionalMemberAccess(expression)};");
                    _lines.Add($"// Consider restructuring to avoid nested value type property assignments");
                }
                else
                {
                    _lines.Add($"{fullDestPath} = {NormalizeConditionalMemberAccess(expression)};");
                }
                break;
        }
    }

    private void ProcessConditionalExpression(ConditionalExpressionSyntax conditional, string fullDestPath)
    {
        var conditionText = conditional.Condition.ToString();
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        ProcessConditional(conditionText, whenTrue, whenFalse, fullDestPath);
    }

    internal List<string> ProcessRootConditionalExpression(ConditionalExpressionSyntax conditional, string currentDestPath)
    {
        _lines.Clear();
        var conditionText = conditional.Condition.WithoutTrivia().ToString();
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
            ProcessNestedConditionalWithBothObjects(conditionText, whenTrue, whenFalse, currentDestPath, _lines, "");
        }

        return _lines.ToList();
    }

    private void ProcessConditional(string conditionText, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse, string fullDestPath)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            _lines.Add($"// Skipping collection property: {fullDestPath}");
            return;
        }

        _lines.Add($"if ({conditionText})");
        _lines.Add("{");

        // Process the true branch
        if (whenTrue is ObjectCreationExpressionSyntax trueObjectCreation)
        {
            ProcessObjectCreationWithIndent(trueObjectCreation, fullDestPath, "    ");
        }
        else if (whenTrue is ThrowExpressionSyntax)
        {
            _lines.Add($"    {whenTrue.WithoutTrivia()};");
        }
        else
        {
            _lines.Add($"    {fullDestPath} = {NormalizeConditionalMemberAccess(whenTrue)};");
        }

        _lines.Add("}");
        _lines.Add("else");
        _lines.Add("{");

        // Process the false branch
        if (whenFalse is ObjectCreationExpressionSyntax falseObjectCreation)
        {
            ProcessObjectCreationWithIndent(falseObjectCreation, fullDestPath, "    ");
        }
        else if (whenFalse is ThrowExpressionSyntax)
        {
            _lines.Add($"    {whenFalse.WithoutTrivia()};");
        }
        else
        {
            _lines.Add($"    {fullDestPath} = {NormalizeConditionalMemberAccess(whenFalse)};");
        }

        _lines.Add("}");
    }

    private void ProcessObjectCreationWithIndent(ObjectCreationExpressionSyntax objectCreation, string fullDestPath, string indent)
    {
        // Ensure target object exists - only add null check if target can be null
        if (typeContext.CanPropertyBeNull(fullDestPath))
        {
            _lines.Add($"{indent}if ({fullDestPath} == null)");
            _lines.Add($"{indent}    {fullDestPath} = new {objectCreation.Type}();");
        }
        else
        {
            // For value types, just assign directly
            _lines.Add($"{indent}{fullDestPath} = new {objectCreation.Type}();");
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

                    ProcessNestedExpression(nestedAssignment.Right, nestedDestPath, _lines, indent);
                }
            }
        }
    }

    private void ProcessNestedExpression(ExpressionSyntax expression, string fullDestPath, List<string> lines, string indent)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
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
                lines.Add($"{indent}{fullDestPath} = {NormalizeConditionalMemberAccess(expression)};");
                break;
        }
    }

    private void ProcessNestedConditional(ConditionalExpressionSyntax conditional, string fullDestPath, List<string> lines, string indent)
    {
        var conditionText = conditional.Condition.ToString();
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;

        ProcessNestedConditionalWithBothObjects(conditionText, whenTrue, whenFalse, fullDestPath, lines, indent);
    }

    private void ProcessNestedConditionalWithBothObjects(string conditionText, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse, string fullDestPath, List<string> lines, string indent)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
            return;
        }

        lines.Add($"{indent}if ({conditionText})");
        lines.Add($"{indent}{{");

        // Process the true branch
        if (whenTrue is ObjectCreationExpressionSyntax trueObjectCreation)
        {
            ProcessNestedObjectCreationInBranch(trueObjectCreation, fullDestPath, lines, indent + "    ");
        }
        else if (whenTrue is ThrowExpressionSyntax)
        {
            lines.Add($"{indent}    {whenTrue.WithoutTrivia()};");
        }
        else
        {
            lines.Add($"{indent}    {fullDestPath} = {NormalizeConditionalMemberAccess(whenTrue)};");
        }

        lines.Add($"{indent}}}");
        lines.Add($"{indent}else");
        lines.Add($"{indent}{{");

        // Process the false branch
        if (whenFalse is ObjectCreationExpressionSyntax falseObjectCreation)
        {
            ProcessNestedObjectCreationInBranch(falseObjectCreation, fullDestPath, lines, indent + "    ");
        }
        else if (whenFalse is ThrowExpressionSyntax)
        {
            lines.Add($"{indent}    {whenFalse.WithoutTrivia()};");
        }
        else
        {
            lines.Add($"{indent}    {fullDestPath} = {NormalizeConditionalMemberAccess(whenFalse)};");
        }

        lines.Add($"{indent}}}");
    }

    private void ProcessNestedObjectCreationInBranch(ObjectCreationExpressionSyntax objectCreation, string fullDestPath, List<string> lines, string indent)
    {
        // Ensure nested target object exists - only add null check if target can be null
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

        // Process nested properties
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

    private void ProcessNestedObjectCreation(ObjectCreationExpressionSyntax objectCreation, string fullDestPath, List<string> lines, string indent)
    {
        // Skip collection properties - they are complex to update safely
        if (typeContext.IsCollectionType(fullDestPath))
        {
            lines.Add($"{indent}// Skipping collection property: {fullDestPath}");
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

    private string NormalizeConditionalMemberAccess(ExpressionSyntax expression)
    {
        // Convert shapes like (conditional)?.WhenNotNull.Tail into a textual form that preserves the
        // conditional access correctly when used on the right-hand side of assignments.

        string Recurse(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case MemberAccessExpressionSyntax ma:
                    var left = Recurse(ma.Expression);
                    var right = ma.Name.ToString();

                    // If left is a conditional access root expressed as "X?{dotChain}", avoid duplicating dots
                    if (left.Contains("?") && left.EndsWith(")") == false && left.Contains("."))
                    {
                        return left + "." + right;
                    }
                    return left + "." + right;

                case ConditionalAccessExpressionSyntax cae:
                    var target = cae.Expression.WithoutTrivia().ToString();
                    var tail = cae.WhenNotNull.WithoutTrivia().ToString(); // typically starts with '.' or '['
                    return target + "?" + tail;

                case MemberBindingExpressionSyntax mbs:
                    // Dot-prefixed fragment without its conditional root; attach to the source parameter
                    return sourceParamName + "?" + mbs.WithoutTrivia().ToString();

                case ElementBindingExpressionSyntax ebs:
                    return sourceParamName + "?" + ebs.WithoutTrivia().ToString();


                case InterpolatedStringExpressionSyntax ise:
                    return FormatInterpolated(ise, Recurse);

                default:
                    return expr.WithoutTrivia().ToString();
            }
        }

        return Recurse(expression);
    }

    private static string FormatInterpolated(InterpolatedStringExpressionSyntax ise, System.Func<ExpressionSyntax, string> normalize)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("$");
        sb.Append('"');
        foreach (var part in ise.Contents)
        {
            switch (part)
            {
                case InterpolatedStringTextSyntax text:
                    var txt = text.TextToken.Text ?? string.Empty;
                    txt = txt.Replace("\"", "\\\"");
                    sb.Append(txt);
                    break;
                case InterpolationSyntax interp:
                    sb.Append("{");
                    sb.Append(normalize(interp.Expression));
                    sb.Append("}");
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
