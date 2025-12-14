using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.SyntaxRewriters;

internal sealed class ParameterSubstitutionRewriter(string paramName, ExpressionSyntax arg)
    : CSharpSyntaxRewriter(true)
{
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        return node.Identifier.Text == paramName ? arg.WithoutTrivia() : base.VisitIdentifierName(node);
    }
}