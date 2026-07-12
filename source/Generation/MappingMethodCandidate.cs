using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace AlephMapper.Generation;

internal static class MappingMethodCandidate
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        return node is MethodDeclarationSyntax { Parent: ClassDeclarationSyntax };
    }
}
