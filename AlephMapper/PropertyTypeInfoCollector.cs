using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper;

/// <summary>
/// Collects property type information from object creation expressions for Updatable method generation.
/// This visitor walks through the syntax tree and gathers type information for each property path.
/// </summary>
internal class PropertyTypeInfoCollector : CSharpSyntaxWalker
{
    private HashSet<ITypeSymbol> _visitedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
    private readonly ITypeSymbol _currentTargetType;
    private readonly string _rootPath;

    /// <summary>
    /// Collects property type information from object creation expressions for Updatable method generation.
    /// This visitor walks through the syntax tree and gathers type information for each property path.
    /// </summary>
    public PropertyTypeInfoCollector(ITypeSymbol currentTargetType, string rootPath)
    {
        _currentTargetType = currentTargetType;
        _rootPath = rootPath;
        TypeContext.AddPropertyType(rootPath, currentTargetType);
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
            // Ensure the root type for this collector is considered visited
            if (_currentTargetType != null)
            {
                _visitedTypes.Add(_currentTargetType);
            }

            var propertyName = assignment.Left.ToString();
            var fullPropertyPath = string.IsNullOrEmpty(_rootPath)
                ? propertyName
                : $"{_rootPath}.{propertyName}";

            // Resolve property type from the known target type to avoid fragile LHS binding in speculative models
            ITypeSymbol? resolvedPropertyType = null;

            if (_currentTargetType is INamedTypeSymbol named)
            {
                var prop = named.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(p => p.Name == propertyName);
                if (prop != null)
                {
                    resolvedPropertyType = prop.Type;
                }
            }

            if (resolvedPropertyType != null)
            {
                TypeContext.AddPropertyType(fullPropertyPath, resolvedPropertyType);
            }

            // Recursively process nested object creations
            ITypeSymbol? nestedTargetType = null;
            if (_currentTargetType is INamedTypeSymbol named2)
            {
                var prop2 = named2.GetMembers()
                                   .OfType<IPropertySymbol>()
                                   .FirstOrDefault(p => p.Name == propertyName);
                nestedTargetType = prop2?.Type;
            }

            if (nestedTargetType != null)
            {
                // Prevent infinite recursion in circular property type graphs
                if (!_visitedTypes.Contains(nestedTargetType))
                {
                    var nestedCollector = new PropertyTypeInfoCollector(nestedTargetType, fullPropertyPath)
                    {
                        TypeContext = TypeContext,
                        _visitedTypes = _visitedTypes
                    };
                    nestedCollector.Visit(assignment.Right);
                }
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
            if (_currentTargetType != null)
            {
                _visitedTypes.Add(_currentTargetType);
            }
            
            var trueCollector = new PropertyTypeInfoCollector(_currentTargetType, _rootPath) { TypeContext = TypeContext, _visitedTypes = _visitedTypes };
            trueCollector.Visit(node.WhenTrue);
            trueCollector.Visit(node.WhenFalse);
        }
        catch
        {
            // Ignore type collection errors and continue processing
        }
    }
}
