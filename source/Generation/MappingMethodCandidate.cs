using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Threading;

namespace AlephMapper.Generation;

internal static class MappingMethodCandidate
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        if (node is not MethodDeclarationSyntax
            {
                Parent: ClassDeclarationSyntax containingClass,
                ExpressionBody: not null
            } method)
        {
            return false;
        }

        if (!containingClass.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        return method.AttributeLists.Count != 0 ||
               containingClass.AttributeLists.Count != 0 ||
               containingClass.Members.OfType<MethodDeclarationSyntax>().Any(static member =>
                   member.AttributeLists.Count != 0) ||
               containingClass.Members.OfType<MethodDeclarationSyntax>().Any(static member =>
                   member.ParameterList.Parameters.Any(static parameter =>
                       parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ThisKeyword)))) ||
               method.ParameterList.Parameters.Any(static parameter =>
                   parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ThisKeyword)));
    }
}
