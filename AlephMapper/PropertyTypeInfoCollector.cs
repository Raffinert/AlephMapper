using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace AlephMapper;

/// <summary>
/// Collects property type information from object creation expressions for Updatable method generation.
/// This visitor walks through the syntax tree and gathers type information for each property path.
/// </summary>
internal class PropertyTypeInfoCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel semanticModel;
    private readonly string rootPath;
    private readonly ITypeSymbol currentTargetType;

    public PropertyTypeInfoCollector(ITypeSymbol currentTargetType, string rootPath)
    {
        this.currentTargetType = currentTargetType;
        this.rootPath = rootPath;
    }
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

            // Resolve property type from the known target type to avoid fragile LHS binding in speculative models
            ITypeSymbol? resolvedPropertyType = null;

            if (currentTargetType is INamedTypeSymbol named)
            {
                var prop = named.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(p => p.Name == propertyName);
                if (prop != null)
                {
                    resolvedPropertyType = prop.Type;
                }
            }

            // Fallback to RHS type if symbol lookup failed
            if (resolvedPropertyType == null)
            {
                var rightTypeInfo = semanticModel.GetTypeInfo(assignment.Right);
                if (rightTypeInfo.Type != null)
                {
                    resolvedPropertyType = rightTypeInfo.Type;
                }
            }

            if (resolvedPropertyType != null)
            {
                TypeContext.AddPropertyType(fullPropertyPath, resolvedPropertyType);
            }

            // Recursively process nested object creations
            ITypeSymbol? nestedTargetType = null;
            if (currentTargetType is INamedTypeSymbol named2)
            {
                var prop2 = named2.GetMembers()
                                   .OfType<IPropertySymbol>()
                                   .FirstOrDefault(p => p.Name == propertyName);
                nestedTargetType = prop2?.Type;
            }

            if (nestedTargetType != null)
            {
                var nestedCollector = new PropertyTypeInfoCollector(nestedTargetType, fullPropertyPath)
                {
                    TypeContext = TypeContext
                };
                nestedCollector.Visit(assignment.Right);
            }
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
            var trueCollector = new PropertyTypeInfoCollector(currentTargetType, rootPath) { TypeContext = TypeContext };
            trueCollector.Visit(node.WhenTrue);
            trueCollector.Visit(node.WhenFalse);
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }
    }
}
