using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.SyntaxRewriters;

internal sealed class ParameterSubstitutionRewriter(IReadOnlyDictionary<string, ExpressionSyntax> substitutions)
    : CSharpSyntaxRewriter(true)
{
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (substitutions.TryGetValue(node.Identifier.Text, out var replacement))
        {
            return replacement.WithoutTrivia();
        }

        return base.VisitIdentifierName(node);
    }
}
