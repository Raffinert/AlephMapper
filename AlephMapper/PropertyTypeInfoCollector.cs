using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

/// <summary>
/// Collects property type information from object creation expressions for Updatable method generation.
/// This visitor walks through the syntax tree and gathers type information for each property path.
/// </summary>
internal class PropertyTypeInfoCollector(SemanticModel semanticModel, string rootPath) : CSharpSyntaxWalker
{
    public PropertyMappingContext TypeContext { get; private set; } = new();

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {

        if (node.Initializer?.Expressions == null)
        {
            return;
        }

        foreach (var expr in node.Initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assignment)
            {
                ProcessAssignment(assignment);
            }
        }
    }

    private void ProcessAssignment(AssignmentExpressionSyntax assignment)
    {
        try
        {
            var propertyName = assignment.Left.ToString();
            var fullPropertyPath = string.IsNullOrEmpty(rootPath)
                ? propertyName
                : $"{rootPath}.{propertyName}";

            // Get type information for the property being assigned

            var leftTypeInfo = semanticModel.GetTypeInfo(assignment.Left);
            if (leftTypeInfo.Type != null)
            {
                TypeContext.AddPropertyType(fullPropertyPath, leftTypeInfo.Type);
            }
            else
            {
                var rightTypeInfo = semanticModel.GetTypeInfo(assignment.Right);
                if (rightTypeInfo.Type != null)
                {
                    TypeContext.AddPropertyType(fullPropertyPath, rightTypeInfo.Type);
                }
            }

            // Recursively process nested object creations
            var nestedCollector = new PropertyTypeInfoCollector(semanticModel, fullPropertyPath) { TypeContext = TypeContext };
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
            var trueCollector = new PropertyTypeInfoCollector(semanticModel, rootPath) { TypeContext = TypeContext };
            trueCollector.Visit(node.WhenTrue);
            trueCollector.Visit(node.WhenFalse);
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }
    }
}