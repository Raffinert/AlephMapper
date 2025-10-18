#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

/// <summary>
/// Rewrites collection expressions to explicit constructor calls for expression tree compatibility.
/// Collection expressions (like []) are not supported in expression trees and cause CS9175.
/// </summary>
internal class CollectionExpressionRewriter(SemanticModel semanticModel) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        // Look for null coalescing expressions with collection expressions on the right
        if (!node.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken)) return base.VisitBinaryExpression(node);
        var right = node.Right;

        // Check if the right side is a collection expression (represented as [] in older Roslyn)
        if (!IsCollectionExpression(right)) return base.VisitBinaryExpression(node);
        var rewrittenRight = RewriteCollectionExpression(right, node.Left);
        return node.WithRight(rewrittenRight);

    }

    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Handle assignment expressions where the right side is a collection expression
        if (!IsCollectionExpression(node.Right)) return base.VisitAssignmentExpression(node);
        var rewritten = RewriteCollectionExpression(node.Right, node.Left);
        return node.WithRight(rewritten);

    }

    public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        // Handle property initializers and variable assignments
        if (!IsCollectionExpression(node.Value)) return base.VisitEqualsValueClause(node);
        var rewritten = RewriteCollectionExpression(node.Value, null);
        return node.WithValue(rewritten);

    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        // Handle method arguments that are collection expressions
        if (!IsCollectionExpression(node.Expression)) return base.VisitArgument(node);
        var rewritten = RewriteCollectionExpression(node.Expression, null);
        return node.WithExpression(rewritten);

    }

    private static bool IsCollectionExpression(SyntaxNode node)
    {
        // Since we're using Microsoft.CodeAnalysis.CSharp version 4.5.0, 
        // we don't have access to CollectionExpressionSyntax.
        // Collection expressions appear as ImplicitArrayCreationExpressionSyntax with empty initializer
        // or they might be represented differently in the syntax tree.

        // Check for various patterns that might represent collection expressions
        if (node is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            // Check if it has an empty initializer (which represents [])
            return implicitArray.Initializer?.Expressions.Count == 0;
        }

        // Check for literal expressions that might be collection expressions
        // In some cases, [] might be parsed as a different kind of syntax node
        if (node is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText == "[]" || literal.ToString().Trim() == "[]";
        }

        // Check for bracketed expressions
        if (node is BracketedArgumentListSyntax)
        {
            return true;
        }

        // Check for expressions that look like empty collection expressions
        var nodeText = node.ToString().Trim();
        return nodeText == "[]";
    }

    private ExpressionSyntax RewriteCollectionExpression(SyntaxNode collectionExpression, SyntaxNode? contextNode)
    {
        // Try to infer the collection type from the context
        var targetType = InferCollectionType(contextNode, collectionExpression);

        // Generate the appropriate constructor call
        return CreateConstructorExpression(targetType, collectionExpression);
    }

    private ITypeSymbol? InferCollectionType(SyntaxNode? contextNode, SyntaxNode collectionExpression)
    {
        try
        {
            // First, try to get the type from the semantic model of the collection expression itself
            var typeInfo = semanticModel.GetTypeInfo(collectionExpression);
            if (typeInfo.Type != null && typeInfo.Type.TypeKind != TypeKind.Error)
            {
                return typeInfo.Type;
            }

            // If that fails, try to infer from the context
            if (contextNode != null)
            {
                // For binary expressions (null coalescing), use the left side type
                var leftTypeInfo = semanticModel.GetTypeInfo(contextNode);
                if (leftTypeInfo.Type != null)
                    return leftTypeInfo.Type;

                // For assignments, get the type of the left side
                if (contextNode.Parent is AssignmentExpressionSyntax assignment)
                {
                    var leftType = semanticModel.GetTypeInfo(assignment.Left);
                    if (leftType.Type != null)
                        return leftType.Type;
                }

                // For property initializers, look at the property type
                if (contextNode.Parent is EqualsValueClauseSyntax equalsValue &&
                    equalsValue.Parent is PropertyDeclarationSyntax property)
                {
                    var propType = semanticModel.GetTypeInfo(property.Type);
                    if (propType.Type != null)
                        return propType.Type;
                }
            }

            // Additional context inference: look at the parent assignment expression
            // Walk up the syntax tree to find assignment context
            var currentNode = collectionExpression.Parent;
            while (currentNode != null)
            {
                // Look for assignment expressions where we're on the right side
                if (currentNode is AssignmentExpressionSyntax assignmentExpr &&
                    IsDescendantOf(collectionExpression, assignmentExpr.Right))
                {
                    var leftSideType = semanticModel.GetTypeInfo(assignmentExpr.Left);
                    if (leftSideType.Type != null)
                        return leftSideType.Type;
                }

                // Look for binary expressions (null coalescing) where we're on the right side  
                if (currentNode is BinaryExpressionSyntax binaryExpr &&
                    binaryExpr.OperatorToken.IsKind(SyntaxKind.QuestionQuestionToken) &&
                    IsDescendantOf(collectionExpression, binaryExpr.Right))
                {
                    var leftSideType = semanticModel.GetTypeInfo(binaryExpr.Left);
                    if (leftSideType.Type != null)
                        return leftSideType.Type;
                }

                currentNode = currentNode.Parent;
            }

            // Fallback: look for common collection interfaces and create List<T>
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDescendantOf(SyntaxNode child, SyntaxNode potentialParent)
    {
        var current = child;
        while (current != null)
        {
            if (current == potentialParent)
                return true;
            current = current.Parent;
        }
        return false;
    }

    private static ExpressionSyntax CreateConstructorExpression(ITypeSymbol? targetType, SyntaxNode originalExpression)
    {
        if (targetType is INamedTypeSymbol namedType)
        {
            // For List<T>, create new List<T>()
            if (CollectionHelper.IsListType(targetType))
            {
                var elementType = CollectionHelper.GetElementType(targetType);
                if (elementType != null)
                {
                    return CreateListConstructor(elementType);
                }
            }

            // For arrays T[], create new T[0] or Array.Empty<T>()
            if (CollectionHelper.IsArrayType(targetType))
            {
                var elementType = CollectionHelper.GetElementType(targetType);
                if (elementType != null)
                {
                    return CreateArrayExpression(elementType);
                }
            }

            // For IEnumerable<T>, ICollection<T>, etc., default to List<T>
            if (CollectionHelper.ImplementsGenericIEnumerable(targetType))
            {
                var elementType = CollectionHelper.GetElementType(targetType);
                if (elementType != null)
                {
                    return CreateListConstructor(elementType);
                }
            }

            // For the exact target type, try to create a constructor
            return CreateGenericConstructor(namedType);
        }

        // Ultimate fallback - create a generic empty list with string type for common scenarios
        return SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.GenericName(SyntaxFactory.Identifier("List"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))
                        )
                    )
                ).WithLeadingTrivia(SyntaxFactory.Space)
        ).WithArgumentList(SyntaxFactory.ArgumentList())
        .WithLeadingTrivia(originalExpression.GetLeadingTrivia())
        .WithTrailingTrivia(originalExpression.GetTrailingTrivia());
    }

    private static ExpressionSyntax CreateListConstructor(ITypeSymbol elementType)
    {
        var elementTypeSyntax = SyntaxFactory.IdentifierName(elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.GenericName(SyntaxFactory.Identifier("List"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(elementTypeSyntax)
                    )
                )
                .WithLeadingTrivia(SyntaxFactory.Space)
        ).WithArgumentList(SyntaxFactory.ArgumentList())
         .WithNewKeyword(SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space));
    }

    private static ExpressionSyntax CreateArrayExpression(ITypeSymbol elementType)
    {
        // Create Array.Empty<T>() for better performance
        var elementTypeSyntax = SyntaxFactory.IdentifierName(elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Array"),
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("Empty"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(elementTypeSyntax)
                        )
                    )
            )
        ).WithArgumentList(SyntaxFactory.ArgumentList());
    }

    private static ExpressionSyntax CreateGenericConstructor(INamedTypeSymbol namedType)
    {
        var typeSyntax = SyntaxFactory.IdentifierName(namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        return SyntaxFactory.ObjectCreationExpression(typeSyntax.WithLeadingTrivia(SyntaxFactory.Space))
            .WithArgumentList(SyntaxFactory.ArgumentList());
    }
}

#nullable restore