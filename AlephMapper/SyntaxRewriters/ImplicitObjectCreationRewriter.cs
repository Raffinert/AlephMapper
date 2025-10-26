using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax implicitNew)
    {
        if (implicitNew.ArgumentList.Arguments.Count > 0)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        string type = model.GetTypeInfo(implicitNew).Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (type == null)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        return ObjectCreationExpression(IdentifierName(type))
            .WithInitializer((InitializerExpressionSyntax)VisitInitializerExpression(implicitNew.Initializer!))
            .WithArgumentList((ArgumentListSyntax)VisitArgumentList(implicitNew.ArgumentList))
            .WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
    }
}