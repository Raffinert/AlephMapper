using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var rewritten = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
        if (model.GetTypeInfo(node).Type is { } typeSymbol)
        {
            var typeName = AlephMapper.Helpers.TypeDisplay.ForSymbol(
                typeSymbol,
                NullableAnnotation.NotAnnotated,
                nullablePolicy);
            rewritten = rewritten.WithType(ParseTypeName(typeName).WithTriviaFrom(rewritten.Type));
        }

        return IsAnnotatedReturnCreation(node)
            ? rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(InlinedReturnCreationAnnotation))
            : rewritten;
    }

    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax implicitNew)
    {
        var typeSymbol = model.GetTypeInfo(implicitNew).Type;
        var type = typeSymbol == null
            ? null
            : AlephMapper.Helpers.TypeDisplay.ForSymbol(
                typeSymbol,
                NullableAnnotation.NotAnnotated,
                nullablePolicy);

        if (type == null)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        var objectCreation = ObjectCreationExpression(ParseTypeName(type).WithTrailingTrivia(ElasticCarriageReturn));

        if (implicitNew.Initializer != null)
        {
            objectCreation = objectCreation.WithInitializer((InitializerExpressionSyntax)VisitInitializerExpression(implicitNew.Initializer));
        }

        if (implicitNew.Initializer == null || implicitNew.ArgumentList.Arguments.Count > 0)
        {
            objectCreation = objectCreation.WithArgumentList((ArgumentListSyntax)VisitArgumentList(implicitNew.ArgumentList));
        }

        objectCreation = objectCreation.WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
        return IsAnnotatedReturnCreation(implicitNew)
            ? objectCreation.WithAdditionalAnnotations(new SyntaxAnnotation(InlinedReturnCreationAnnotation))
            : objectCreation;
    }

    private bool IsAnnotatedReturnCreation(ExpressionSyntax expression)
    {
        return returnTypeToAnnotate != null &&
               SymbolEqualityComparer.Default.Equals(
                   model.GetTypeInfo(expression).Type ?? model.GetTypeInfo(expression).ConvertedType,
                   returnTypeToAnnotate);
    }
}
