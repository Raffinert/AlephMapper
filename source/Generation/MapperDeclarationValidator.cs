using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.Generation;

internal static class MapperDeclarationValidator
{
    public const string RequiredDeclaration = "the mapper must be a static partial class and every enclosing type must be a partial class";

    public static bool IsEligible(INamedTypeSymbol mapper)
    {
        if (!mapper.IsStatic || !IsPartialClass(mapper)) return false;
        for (var containing = mapper.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (!IsPartialClass(containing)) return false;
        }
        return true;
    }

    private static bool IsPartialClass(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Length > 0 && type.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax() is ClassDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
}
