#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using AlephMapper.SyntaxRewriters;
using AlephMapper.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Adaptation;

/// <summary>
/// Replaces only destination creations identified from the original semantic tree.
/// This avoids relying on type spelling after inlining detaches syntax nodes.
/// </summary>
internal sealed class AdaptedDestinationRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<TextSpan> _destinationCreationSpans;
    private readonly HashSet<string> _destinationCreationTypeTexts;
    private readonly TypeSyntax _adaptedDestinationType;
    private readonly ITypeSymbol _adaptedDestinationTypeSymbol;
    private readonly NullablePolicy _nullablePolicy;
    private readonly HashSet<string> _originalDestinationTypeTexts;
    private readonly ExpressionSyntax _root;
    private readonly Stack<ITypeSymbol> _objectInitializerTypeStack = new();
    private readonly Stack<ITypeSymbol> _expectedExpressionTypeStack = new();

    private AdaptedDestinationRewriter(
        IEnumerable<TextSpan> destinationCreationSpans,
        IEnumerable<string> destinationCreationTypeTexts,
        IEnumerable<string> originalDestinationTypeTexts,
        string adaptedDestinationTypeName,
        ITypeSymbol adaptedDestinationTypeSymbol,
        NullablePolicy nullablePolicy,
        ExpressionSyntax root)
    {
        _destinationCreationSpans = new HashSet<TextSpan>(destinationCreationSpans);
        _destinationCreationTypeTexts = new HashSet<string>(destinationCreationTypeTexts);
        _adaptedDestinationType = SyntaxFactory.ParseTypeName(adaptedDestinationTypeName);
        _adaptedDestinationTypeSymbol = adaptedDestinationTypeSymbol;
        _nullablePolicy = nullablePolicy;
        _originalDestinationTypeTexts = new HashSet<string>(originalDestinationTypeTexts);
        _root = root;
    }

    public static ExpressionSyntax Rewrite(
        ExpressionSyntax originalBody,
        ExpressionSyntax bodyToRewrite,
        SemanticModel semanticModel,
        ITypeSymbol originalDestinationType,
        string adaptedDestinationTypeName,
        ITypeSymbol adaptedDestinationType,
        NullablePolicy nullablePolicy)
    {
        if (bodyToRewrite is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName(adaptedDestinationTypeName),
                    implicitCreation.ArgumentList ?? SyntaxFactory.ArgumentList(),
                    implicitCreation.Initializer)
                .WithTriviaFrom(implicitCreation);
        }

        if (originalBody is ImplicitObjectCreationExpressionSyntax && bodyToRewrite is ObjectCreationExpressionSyntax objectCreation)
        {
            return objectCreation.WithType(SyntaxFactory.ParseTypeName(adaptedDestinationTypeName).WithTriviaFrom(objectCreation.Type));
        }

        var destinationCreations = originalBody.DescendantNodesAndSelf()
            .OfType<ExpressionSyntax>()
            .Where(node => node is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            .Where(node => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetTypeInfo(node).Type ?? semanticModel.GetTypeInfo(node).ConvertedType,
                originalDestinationType))
            .ToArray();

        var spans = destinationCreations.Select(node => node.Span);
        var typeTexts = destinationCreations
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(node => node.Type.ToString());
        var originalDestinationTypeTexts = GetTypeTextCandidates(originalDestinationType);

        return (ExpressionSyntax)new AdaptedDestinationRewriter(
                spans,
                typeTexts,
                originalDestinationTypeTexts,
                adaptedDestinationTypeName,
                adaptedDestinationType,
                nullablePolicy,
                bodyToRewrite)
            .Visit(bodyToRewrite)!;
    }

    public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var targetType = GetObjectCreationTargetType(node);
        var typeSyntax = targetType == null
            ? null
            : SyntaxFactory.ParseTypeName(GetObjectCreationTypeName(targetType)).WithTriviaFrom(node.Type);

        if (targetType != null)
        {
            _objectInitializerTypeStack.Push(targetType);
        }

        try
        {
            var argumentList = node.ArgumentList == null ? null : (ArgumentListSyntax?)Visit(node.ArgumentList);
            var initializer = node.Initializer == null ? null : (InitializerExpressionSyntax?)Visit(node.Initializer);
            return node
                .WithArgumentList(argumentList)
                .WithInitializer(initializer)
                .WithType(typeSyntax ?? node.Type);
        }
        finally
        {
            if (targetType != null)
            {
                _objectInitializerTypeStack.Pop();
            }
        }
    }

    public override SyntaxNode VisitCastExpression(CastExpressionSyntax node)
    {
        var rewritten = (CastExpressionSyntax)base.VisitCastExpression(node)!;
        var targetType = GetExpectedExpressionType();
        return targetType != null && IsAdaptableObjectType(targetType) && !_originalDestinationTypeTexts.Contains(node.Type.ToString())
            ? rewritten.WithType(SyntaxFactory.ParseTypeName(GetCastTypeName(targetType)).WithTriviaFrom(rewritten.Type))
            : _originalDestinationTypeTexts.Contains(node.Type.ToString())
                ? rewritten.WithType(SyntaxFactory.ParseTypeName(GetCastTypeName(_adaptedDestinationTypeSymbol)).WithTriviaFrom(rewritten.Type))
                : rewritten;
    }

    public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        var left = (ExpressionSyntax)Visit(node.Left)!;
        var expectedType = TryGetAssignedMemberType(node.Left);
        if (expectedType != null)
        {
            _expectedExpressionTypeStack.Push(expectedType);
        }

        try
        {
            var right = (ExpressionSyntax)Visit(node.Right)!;
            return node.WithLeft(left).WithRight(right);
        }
        finally
        {
            if (expectedType != null)
            {
                _expectedExpressionTypeStack.Pop();
            }
        }
    }

    public override SyntaxNode VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        var condition = (ExpressionSyntax)Visit(node.Condition)!;
        var expectedType = GetExpectedExpressionType();
        if (expectedType != null)
        {
            _expectedExpressionTypeStack.Push(expectedType);
        }

        try
        {
            return node
                .WithCondition(condition)
                .WithWhenTrue((ExpressionSyntax)Visit(node.WhenTrue)!)
                .WithWhenFalse((ExpressionSyntax)Visit(node.WhenFalse)!);
        }
        finally
        {
            if (expectedType != null)
            {
                _expectedExpressionTypeStack.Pop();
            }
        }
    }

    public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        return VisitLambdaWithoutOuterExpectedType(node, base.VisitSimpleLambdaExpression);
    }

    public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        return VisitLambdaWithoutOuterExpectedType(node, base.VisitParenthesizedLambdaExpression);
    }

    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        var rewritten = (ImplicitObjectCreationExpressionSyntax)base.VisitImplicitObjectCreationExpression(node)!;
        var targetType = _destinationCreationSpans.Contains(node.Span) || ReferenceEquals(node, _root)
            ? _adaptedDestinationTypeSymbol
            : GetExpectedExpressionType();

        return targetType != null && IsAdaptableObjectType(targetType)
            ? SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName(GetObjectCreationTypeName(targetType)),
                    rewritten.ArgumentList ?? SyntaxFactory.ArgumentList(),
                    rewritten.Initializer)
                .WithTriviaFrom(rewritten)
            : rewritten;
    }

    private ITypeSymbol? GetObjectCreationTargetType(ObjectCreationExpressionSyntax node)
    {
        if (_destinationCreationSpans.Contains(node.Span) ||
            _destinationCreationTypeTexts.Contains(node.Type.ToString()) ||
            _originalDestinationTypeTexts.Contains(node.Type.ToString()))
        {
            return _adaptedDestinationTypeSymbol;
        }

        var expectedType = GetExpectedExpressionType();
        return expectedType != null && IsAdaptableObjectType(expectedType) ? expectedType : null;
    }

    private ITypeSymbol? GetExpectedExpressionType()
    {
        return _expectedExpressionTypeStack.Count == 0 ? null : _expectedExpressionTypeStack.Peek();
    }

    private ITypeSymbol? TryGetAssignedMemberType(ExpressionSyntax left)
    {
        if (_objectInitializerTypeStack.Count == 0)
        {
            return null;
        }

        var memberName = left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (memberName == null)
        {
            return null;
        }

        var currentType = _objectInitializerTypeStack.Peek();
        var member = GetMembersIncludingBaseTypes(currentType, memberName)
            .FirstOrDefault(symbol => symbol is IPropertySymbol or IFieldSymbol);
        return member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
    }

    private static IEnumerable<ISymbol> GetMembersIncludingBaseTypes(ITypeSymbol type, string name)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                yield return member;
            }
        }
    }

    private string GetObjectCreationTypeName(ITypeSymbol type)
    {
        return TypeDisplay.ForSymbol(type, NullableAnnotation.NotAnnotated, _nullablePolicy);
    }

    private string GetCastTypeName(ITypeSymbol type)
    {
        var annotation = type.NullableAnnotation == NullableAnnotation.Annotated
            ? NullableAnnotation.Annotated
            : NullableAnnotation.NotAnnotated;
        return TypeDisplay.ForSymbol(type, annotation, _nullablePolicy);
    }

    private static bool IsAdaptableObjectType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class &&
               !type.IsAbstract &&
               type is INamedTypeSymbol { IsGenericType: false };
    }

    private static HashSet<string> GetTypeTextCandidates(ITypeSymbol type)
    {
        var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var candidates = new HashSet<string>(System.StringComparer.Ordinal)
        {
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            fullyQualifiedName,
            fullyQualifiedName.Replace("global::", string.Empty)
        };

        if (type is INamedTypeSymbol { TypeArguments.Length: 0 } namedType)
        {
            candidates.Add(namedType.Name);
            candidates.Add(namedType.Name + "?");
        }

        candidates.Add(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "?");
        candidates.Add(fullyQualifiedName + "?");
        candidates.Add(fullyQualifiedName.Replace("global::", string.Empty) + "?");

        return candidates;
    }

    private T VisitLambdaWithoutOuterExpectedType<T>(T node, Func<T, SyntaxNode?> visit)
        where T : LambdaExpressionSyntax
    {
        if (_expectedExpressionTypeStack.Count == 0)
        {
            return (T)visit(node)!;
        }

        var expectedTypes = _expectedExpressionTypeStack.Reverse().ToArray();
        _expectedExpressionTypeStack.Clear();

        try
        {
            return (T)visit(node)!;
        }
        finally
        {
            foreach (var expectedType in expectedTypes)
            {
                _expectedExpressionTypeStack.Push(expectedType);
            }
        }
    }
}
