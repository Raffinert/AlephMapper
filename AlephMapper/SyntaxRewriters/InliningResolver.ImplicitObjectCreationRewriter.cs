using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax implicitNew)
    {
        var type = model.GetTypeInfo(implicitNew).Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (type == null)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        var objectCreation = ObjectCreationExpression(IdentifierName(type).WithTrailingTrivia(ElasticCarriageReturn));

        if (implicitNew.Initializer != null)
        {
            objectCreation = objectCreation.WithInitializer((InitializerExpressionSyntax)VisitInitializerExpression(implicitNew.Initializer));
        }

        if (implicitNew.ArgumentList.Arguments.Count > 0)
        {
            objectCreation = objectCreation.WithArgumentList((ArgumentListSyntax)VisitArgumentList(implicitNew.ArgumentList));
        }

        return objectCreation.WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
    }
}