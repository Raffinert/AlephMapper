#nullable enable

using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    private TypeSyntax GetFullyQualifiedTypeSyntax(TypeSyntax originalType, TypeSyntax rewrittenType)
    {
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
