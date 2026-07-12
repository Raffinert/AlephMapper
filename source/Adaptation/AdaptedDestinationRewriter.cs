#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
    private readonly ExpressionSyntax _root;

    private AdaptedDestinationRewriter(
        IEnumerable<TextSpan> destinationCreationSpans,
        IEnumerable<string> destinationCreationTypeTexts,
        string adaptedDestinationTypeName,
        ExpressionSyntax root)
    {
        _destinationCreationSpans = new HashSet<TextSpan>(destinationCreationSpans);
        _destinationCreationTypeTexts = new HashSet<string>(destinationCreationTypeTexts);
        _adaptedDestinationType = SyntaxFactory.ParseTypeName(adaptedDestinationTypeName);
        _root = root;
    }

    public static ExpressionSyntax Rewrite(
        ExpressionSyntax originalBody,
        ExpressionSyntax bodyToRewrite,
        SemanticModel semanticModel,
        ITypeSymbol originalDestinationType,
        string adaptedDestinationTypeName)
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

        return (ExpressionSyntax)new AdaptedDestinationRewriter(spans, typeTexts, adaptedDestinationTypeName, bodyToRewrite)
            .Visit(bodyToRewrite)!;
    }

    public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var rewritten = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
        return _destinationCreationSpans.Contains(node.Span) || _destinationCreationTypeTexts.Contains(node.Type.ToString())
            ? rewritten.WithType(_adaptedDestinationType.WithTriviaFrom(rewritten.Type))
            : rewritten;
    }

    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        var rewritten = (ImplicitObjectCreationExpressionSyntax)base.VisitImplicitObjectCreationExpression(node)!;
        return _destinationCreationSpans.Contains(node.Span) || ReferenceEquals(node, _root)
            ? SyntaxFactory.ObjectCreationExpression(_adaptedDestinationType, rewritten.ArgumentList ?? SyntaxFactory.ArgumentList(), rewritten.Initializer)
                .WithTriviaFrom(rewritten)
            : rewritten;
    }
}
