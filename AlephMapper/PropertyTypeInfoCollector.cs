using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

/// <summary>
/// Collects property type information from object creation expressions for Updatable method generation.
/// This visitor walks through the syntax tree and gathers type information for each property path.
/// </summary>
internal class PropertyTypeInfoCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly PropertyMappingContext _typeContext;
    private readonly string _currentPath;

    public PropertyTypeInfoCollector(SemanticModel semanticModel, PropertyMappingContext typeContext, string rootPath = "")
    {
        _semanticModel = semanticModel;
        _typeContext = typeContext;
        _currentPath = rootPath;
    }

    public static PropertyMappingContext CollectTypeInformation(ExpressionSyntax expression, SemanticModel semanticModel, string destPrefix)
    {
        var typeContext = new PropertyMappingContext();

        // Skip type collection if semantic model is null
        if (semanticModel == null)
        {
            return typeContext;
        }

        try
        {
            var collector = new PropertyTypeInfoCollector(semanticModel, typeContext, destPrefix);
            collector.Visit(expression);
        }
        catch
        {
            // If there's any issue with type collection, return empty context
            // This ensures the generator doesn't fail
            return new PropertyMappingContext();
        }

        return typeContext;
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        try
        {
            // Get type information for the object being created
            var typeInfo = _semanticModel.GetTypeInfo(node);
            if (typeInfo.Type != null)
            {
                _typeContext.AddPropertyType(_currentPath, typeInfo.Type);
            }

            // Process the initializer if present
            if (node.Initializer?.Expressions != null)
            {
                foreach (var expr in node.Initializer.Expressions)
                {
                    if (expr is AssignmentExpressionSyntax assignment)
                    {
                        ProcessAssignment(assignment);
                    }
                }
            }
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }

        base.VisitObjectCreationExpression(node);
    }

    private void ProcessAssignment(AssignmentExpressionSyntax assignment)
    {
        try
        {
            var propertyName = assignment.Left.ToString();
            var fullPropertyPath = string.IsNullOrEmpty(_currentPath)
                ? propertyName
                : $"{_currentPath}.{propertyName}";

            // Get type information for the property being assigned

            var leftTypeInfo = _semanticModel.GetTypeInfo(assignment.Left);
            if (leftTypeInfo.Type != null)
            {
                _typeContext.AddPropertyType(fullPropertyPath, leftTypeInfo.Type);
            }
            else
            {
                var rightTypeInfo = _semanticModel.GetTypeInfo(assignment.Right);
                if (rightTypeInfo.Type != null)
                {
                    _typeContext.AddPropertyType(fullPropertyPath, rightTypeInfo.Type);
                }
            }

            // Recursively process nested object creations
            var nestedCollector = new PropertyTypeInfoCollector(_semanticModel, _typeContext, fullPropertyPath);
            nestedCollector.Visit(assignment.Right);
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        try
        {
            // For conditional expressions, we need to analyze both the true and false branches
            // to understand what types are being assigned

            var trueCollector = new PropertyTypeInfoCollector(_semanticModel, _typeContext, _currentPath);
            trueCollector.Visit(node.WhenTrue);

            var falseCollector = new PropertyTypeInfoCollector(_semanticModel, _typeContext, _currentPath);
            falseCollector.Visit(node.WhenFalse);
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }

        base.VisitConditionalExpression(node);
    }
}