#nullable enable

using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
    {
        var rewritten = (CastExpressionSyntax?)base.VisitCastExpression(node);
        return rewritten?.WithType(GetFullyQualifiedTypeSyntax(node.Type, rewritten.Type));
    }

    public override SyntaxNode? VisitTypeOfExpression(TypeOfExpressionSyntax node)
    {
        var rewritten = (TypeOfExpressionSyntax?)base.VisitTypeOfExpression(node);
        return rewritten?.WithType(GetFullyQualifiedTypeSyntax(node.Type, rewritten.Type));
    }

    public override SyntaxNode? VisitDefaultExpression(DefaultExpressionSyntax node)
    {
        var rewritten = (DefaultExpressionSyntax?)base.VisitDefaultExpression(node);
        return rewritten?.WithType(GetFullyQualifiedTypeSyntax(node.Type, rewritten.Type));
    }

    public override SyntaxNode? VisitDeclarationPattern(DeclarationPatternSyntax node)
    {
        var rewritten = (DeclarationPatternSyntax?)base.VisitDeclarationPattern(node);
        return rewritten?.WithType(GetFullyQualifiedTypeSyntax(node.Type, rewritten.Type));
    }

    public override SyntaxNode? VisitRecursivePattern(RecursivePatternSyntax node)
    {
        var rewritten = (RecursivePatternSyntax?)base.VisitRecursivePattern(node);
        return rewritten?.WithType(
            rewritten.Type == null
                ? null
                : GetFullyQualifiedTypeSyntax(node.Type!, rewritten.Type));
    }

    public override SyntaxNode? VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        var rewritten = (ArrayCreationExpressionSyntax?)base.VisitArrayCreationExpression(node);
        return rewritten?.WithType(
            rewritten.Type.WithElementType(
                GetFullyQualifiedTypeSyntax(node.Type.ElementType, rewritten.Type.ElementType)));
    }

    public override SyntaxNode? VisitSizeOfExpression(SizeOfExpressionSyntax node)
    {
        var rewritten = (SizeOfExpressionSyntax?)base.VisitSizeOfExpression(node);
        return rewritten?.WithType(GetFullyQualifiedTypeSyntax(node.Type, rewritten.Type));
    }

    public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
    {
        var rewritten = (GenericNameSyntax?)base.VisitGenericName(node);
        if (rewritten == null)
        {
            return null;
        }

        var typeArguments = node.TypeArgumentList.Arguments
            .Zip(rewritten.TypeArgumentList.Arguments, GetFullyQualifiedTypeSyntax)
            .ToList();
        return rewritten.WithTypeArgumentList(
            rewritten.TypeArgumentList.WithArguments(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList(typeArguments)));
    }

    private TypeSyntax GetFullyQualifiedTypeSyntax(TypeSyntax originalType, TypeSyntax rewrittenType)
    {
        if (!CanQuerySemanticModel(originalType))
        {
            return rewrittenType;
        }

        var type = model.GetTypeInfo(originalType).Type;
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            return rewrittenType;
        }

        var typeName = TypeDisplay.ForSymbol(type, type.NullableAnnotation, nullablePolicy);
        return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseTypeName(typeName)
            .WithTriviaFrom(rewrittenType);
    }
}

#nullable restore
