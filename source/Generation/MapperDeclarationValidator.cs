using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.Generation;

internal static class MapperDeclarationValidator
{
    public const string RequiredDeclaration = "the containing mapper class must be static partial";
    public static bool IsEligible(INamedTypeSymbol mapper) => mapper.IsStatic && mapper.DeclaringSyntaxReferences.All(reference =>
        reference.GetSyntax() is ClassDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
}
