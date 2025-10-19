using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Contains information about a circular reference detected during inlining
/// </summary>
internal class CircularReferenceInfo(IMethodSymbol method, IEnumerable<IMethodSymbol> callStack)
{
    public IMethodSymbol Method { get; } = method;
    public string CallChain { get; } = string.Join(" -> ", callStack.Select(m => $"{m.ContainingType.Name}.{m.Name}"));
}

internal sealed class InliningResolver(SemanticModel model, IDictionary<IMethodSymbol, MappingModel> catalog)
    : CSharpSyntaxRewriter
{
    private readonly HashSet<IMethodSymbol> _callStack = new(SymbolEqualityComparer.Default);
    private readonly List<CircularReferenceInfo> _circularReferences = [];

    /// <summary>
    /// Gets any circular references detected during inlining
    /// </summary>
    public IReadOnlyList<CircularReferenceInfo> CircularReferences => _circularReferences;

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

    private bool IsCircularReference(IMethodSymbol method)
    {
        return _callStack.Contains(method);
    }

    private void RecordCircularReference(IMethodSymbol method)
    {
        var circularRef = new CircularReferenceInfo(method, _callStack.Append(method));
        _circularReferences.Add(circularRef);
    }

    public override SyntaxNode VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax implicitNew)
    {
        if (implicitNew.ArgumentList.Arguments.Count > 0)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        var type = model.GetTypeInfo(implicitNew).Type;

        if (type == null)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        return ObjectCreationExpression(IdentifierName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                    .WithInitializer((InitializerExpressionSyntax)VisitInitializerExpression(implicitNew.Initializer!))
                    .WithArgumentList((ArgumentListSyntax)VisitArgumentList(implicitNew.ArgumentList))
                    .WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
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
                    // Check for circular reference
                    if (IsCircularReference(normalizedMethod))
                    {
                        RecordCircularReference(normalizedMethod);
                        // Return original node without inlining to break the cycle
                        return base.VisitInvocationExpression(node);
                    }

                    var delInvoke = TryGetDelegateInvoke(invokedMethod, 0);
                    if (delInvoke is { Parameters.Length: 1 })
                    {
                        var paramName = string.IsNullOrEmpty(callee.ParamName) ? "x" : callee.ParamName;
                        var lambdaParam = Parameter(Identifier(paramName));

                        // Add method to call stack before inlining
                        _callStack.Add(normalizedMethod);
                        try
                        {
                            var inlinedBody = (ExpressionSyntax)Visit(callee.BodySyntax);
                            var substitutedBody = (ExpressionSyntax)new ParameterSubstitutionRewriter(callee.ParamName, IdentifierName(paramName))
                                    .Visit(inlinedBody)!
                                    .WithoutTrivia();

                            var lambda = SimpleLambdaExpression(lambdaParam, substitutedBody);
                            var newArgs = SeparatedList([arg.WithExpression(lambda)]);
                            return node.WithArgumentList(node.ArgumentList.WithArguments(newArgs));
                        }
                        finally
                        {
                            // Remove method from call stack after inlining
                            _callStack.Remove(normalizedMethod);
                        }
                    }
                }
            }
        }

        // Direct-call inlining (MapToDto(s) -> inline)
        var directCallMethod = SymbolHelpers.Normalize(invokedMethod);
        if (!catalog.TryGetValue(directCallMethod, out var callee2))
        {
            return base.VisitInvocationExpression(node)?.WithoutTrivia();
        }

        // Check for circular reference
        if (IsCircularReference(directCallMethod))
        {
            RecordCircularReference(directCallMethod);
            // Return original node without inlining to break the cycle
            return base.VisitInvocationExpression(node)?.WithoutTrivia();
        }

        // Add method to call stack before inlining
        _callStack.Add(directCallMethod);
        try
        {
            var inlinedBody2 = Visit(callee2.BodySyntax);
            var substituted = new ParameterSubstitutionRewriter(callee2.ParamName, argExpr)
                .Visit(inlinedBody2)
                ?.WithoutTrivia();

            return substituted;
        }
        finally
        {
            // Remove method from call stack after inlining
            _callStack.Remove(directCallMethod);
        }
    }
}