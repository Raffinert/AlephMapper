using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper;

internal sealed class CommentRemover : CSharpSyntaxRewriter
{
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
    {
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            return default;

        return base.VisitTrivia(trivia);
    }
}

internal sealed class ParameterSubstitutionRewriter(string paramName, ExpressionSyntax arg)
    : CSharpSyntaxRewriter(true)
{
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        return node.Identifier.Text == paramName ? arg.WithoutTrivia() : base.VisitIdentifierName(node);
    }
}

internal sealed class InliningResolver(SemanticModel model, IDictionary<IMethodSymbol, MappingModel> catalog)
    : CSharpSyntaxRewriter
{
    private IMethodSymbol ResolveMethodGroupSymbol(ExpressionSyntax expr)
    {
        var si = model.GetSymbolInfo(expr);
        if (si.Symbol is IMethodSymbol ms) return ms;
        return null;
    }

    private static IMethodSymbol TryGetDelegateInvoke(IMethodSymbol invokedMethod, int argIndex)
    {
        if (argIndex < 0 || argIndex >= invokedMethod.Parameters.Length) return null;
        var p = invokedMethod.Parameters[argIndex].Type as INamedTypeSymbol;
        return p?.DelegateInvokeMethod;
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (model.GetSymbolInfo(node.Expression).Symbol is not IMethodSymbol invokedMethod)
        {
            return base.VisitInvocationExpression(node);
        }

        var args = node.ArgumentList.Arguments;

        if (args.Count != 1)
        {
            return base.VisitInvocationExpression(node);
        }

        var arg = args[0];
        var argExpr = arg.Expression;

        // Handle method-group arguments first (Select(MapToX))
        if (argExpr is IdentifierNameSyntax or MemberAccessExpressionSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax)
        {
            var methodGroup = ResolveMethodGroupSymbol(argExpr);

            if (methodGroup is { Parameters.Length: 1 })
            {
                var normalizedMethod = SymbolHelpers.Normalize(methodGroup);

                if (catalog.TryGetValue(normalizedMethod, out var callee))
                {
                    var delInvoke = TryGetDelegateInvoke(invokedMethod, 0);
                    if (delInvoke is { Parameters.Length: 1 })
                    {
                        var paramName = string.IsNullOrEmpty(callee.ParamName) ? "x" : callee.ParamName;
                        var lambdaParam = Parameter(Identifier(paramName));

                        var inlinedBody = (ExpressionSyntax)Visit(callee.BodySyntax);
                        var substitutedBody = (ExpressionSyntax)new ParameterSubstitutionRewriter(callee.ParamName, IdentifierName(paramName))
                                .Visit(inlinedBody)!
                                .WithoutTrivia();

                        var lambda = SimpleLambdaExpression(lambdaParam, substitutedBody);
                        var newArgs = SeparatedList([arg.WithExpression(lambda)]);
                        return node.WithArgumentList(node.ArgumentList.WithArguments(newArgs));
                    }
                }
            }
        }

        // Direct-call inlining (MapToDto(s) -> inline)
        var directCallMethod = SymbolHelpers.Normalize(invokedMethod);
        if (!catalog.TryGetValue(directCallMethod, out var callee2))
        {
            return base.VisitInvocationExpression(node);
        }

        var inlinedBody2 = Visit(callee2.BodySyntax);
        var substituted = new ParameterSubstitutionRewriter(callee2.ParamName, argExpr)
            .Visit(inlinedBody2);

        return substituted;
    }
}