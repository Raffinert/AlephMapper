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
        foreach (var c in si.CandidateSymbols)
        {
            if (c is IMethodSymbol mm) return mm;
        }
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
        node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        if (node == null) return null;
        if (node.Parent == null) return node;

        // Handle method-group arguments first (Select(MapToX))
        var invokedSym = model.GetSymbolInfo(node.Expression).Symbol as IMethodSymbol;
        var args = node.ArgumentList.Arguments;

        if (invokedSym != null && args.Count > 0)
        {
            var newArgs = args;
            var changed = false;

            for (int i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                var expr = arg.Expression;
                if (!(expr is IdentifierNameSyntax
                      || expr is MemberAccessExpressionSyntax
                      || expr is GenericNameSyntax
                      || expr is QualifiedNameSyntax
                      || expr is AliasQualifiedNameSyntax))
                    continue;

                var mg = ResolveMethodGroupSymbol(expr);
                if (mg == null) continue;

                var normalized = SymbolHelpers.Normalize(mg);
                if (!catalog.TryGetValue(normalized, out var callee)) continue;

                if (callee.MethodSymbol.Parameters.Length != 1) continue;

                var delInvoke = TryGetDelegateInvoke(invokedSym, i);
                if (delInvoke != null && delInvoke.Parameters.Length != 1) continue;

                var paramName = string.IsNullOrEmpty(callee.ParamName) ? "x" : callee.ParamName;
                var lambdaParam = Parameter(Identifier(paramName));
                var substitutedBody = (ExpressionSyntax)new ParameterSubstitutionRewriter(callee.ParamName, IdentifierName(paramName))
                    .Visit(callee.BodySyntax);

                var inlinedBody = (ExpressionSyntax)Visit(substitutedBody).WithoutTrivia();
                var lambda = SimpleLambdaExpression(lambdaParam, inlinedBody);

                newArgs = newArgs.Replace(arg, arg.WithExpression(lambda));
                changed = true;
            }

            if (changed)
            {
                node = node.WithArgumentList(node.ArgumentList.WithArguments(newArgs));
            }
        }

        if (node.Parent == null) return node;

        // Direct-call inlining (MapToDto(s) -> inline)
        var callSym = model.GetSymbolInfo(node.Expression).Symbol as IMethodSymbol;
        if (callSym == null) return node;

        var key = SymbolHelpers.Normalize(callSym);
        if (!catalog.TryGetValue(key, out var callee2)) return node;
        if (callee2.MethodSymbol.Parameters.Length != 1) return node;
        if (node.ArgumentList.Arguments.Count != 1) return node;

        var argExpr = node.ArgumentList.Arguments[0].Expression;
        var rewrittenBody = (ExpressionSyntax)Visit(callee2.BodySyntax);

        var substituted = (ExpressionSyntax)new ParameterSubstitutionRewriter(callee2.ParamName, argExpr)
            .Visit(rewrittenBody);

        return substituted;
    }
}