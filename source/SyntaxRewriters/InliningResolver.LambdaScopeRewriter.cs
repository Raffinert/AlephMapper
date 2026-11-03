#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var count = 0;
        try
        {
            var parameter = RenameLambdaParameter(node.Parameter, ref count);
            return node.WithParameter(parameter).WithBody((CSharpSyntaxNode)Visit(node.Body)!);
        }
        finally { while (count-- > 0) _lambdaVariables.Pop(); }
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        var count = 0;
        try
        {
            var parameters = node.ParameterList.Parameters.Select(p => RenameLambdaParameter(p, ref count)).ToArray();
            return node.WithParameterList(node.ParameterList.WithParameters(SeparatedList(parameters)))
                .WithBody((CSharpSyntaxNode)Visit(node.Body)!);
        }
        finally { while (count-- > 0) _lambdaVariables.Pop(); }
    }

    private ParameterSyntax RenameLambdaParameter(ParameterSyntax parameter, ref int count)
    {
        if (parameterSubstitutions is null || !CanQuerySemanticModel(parameter) ||
            model.GetDeclaredSymbol(parameter) is not IParameterSymbol symbol ||
            !parameterSubstitutions.Values.Any(v => v.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(n => n.Identifier.ValueText == symbol.Name)))
            return (ParameterSyntax)base.VisitParameter(parameter)!;
        var used = new HashSet<string>(parameter.SyntaxTree.GetRoot().DescendantTokens()
            .Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText), StringComparer.Ordinal);
        used.UnionWith(parameterSubstitutions.Values.SelectMany(v => v.DescendantTokens())
            .Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Select(t => t.ValueText));
        used.UnionWith(_lambdaVariables.Select(v => ((IdentifierNameSyntax)v.Replacement).Identifier.ValueText));
        var index = 0;
        string name;
        do { name = "__amLambda" + index++; } while (used.Contains(name));
        _lambdaVariables.Push((symbol, IdentifierName(name)));
        count++;
        return ((ParameterSyntax)base.VisitParameter(parameter)!).WithIdentifier(Identifier(name).WithTriviaFrom(parameter.Identifier));
    }
}
