using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver(
    SemanticModel model,
    IDictionary<IMethodSymbol, MappingModel> catalog,
    bool forUpdateMethod,
    NullConditionalRewrite rewriteSupport)
    : CSharpSyntaxRewriter
{
    private HashSet<IMethodSymbol> _callStack = new(SymbolEqualityComparer.Default);
    private List<CircularReferenceInfo> _circularReferences = [];
    private Dictionary<IMethodSymbol, MappingModel> _inlinedMethods = new(SymbolEqualityComparer.Default);
    public IEnumerable<string> UsingDirectives => _inlinedMethods.SelectMany(il => il.Value.UsingDirectives).Distinct();

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

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Parent == null || model.GetSymbolInfo(node.Expression).Symbol is not IMethodSymbol invokedMethod)
        {
            return base.VisitInvocationExpression(node);
        }

        var args = node.ArgumentList.Arguments;
        var firstArgExpression = args.Count > 0 ? args[0].Expression : null;
        var isConditionalAccess = node.Parent is ConditionalAccessExpressionSyntax;

        // Handle method-group arguments first (Select(MapToX))
        if (!isConditionalAccess && firstArgExpression is IdentifierNameSyntax or MemberAccessExpressionSyntax or GenericNameSyntax or QualifiedNameSyntax
            or AliasQualifiedNameSyntax)
        {
            var methodGroup = ResolveMethodGroupSymbol(firstArgExpression);

            if (methodGroup is { Parameters.Length: > 0 })
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
                    if (delInvoke is { Parameters.Length: > 0 } && delInvoke.Parameters.Length == callee.MethodSymbol.Parameters.Length)
                    {
                        var lambdaParams = callee.Parameters
                            .Select(p => Parameter(Identifier(p.Name)))
                            .ToArray();

                        // Add method to call stack before inlining
                        _callStack.Add(normalizedMethod);
                        try
                        {
                            _inlinedMethods[normalizedMethod] = callee;
                            var inlinedBody =
                                (ExpressionSyntax)new InliningResolver(callee.SemanticModel, catalog, forUpdateMethod, rewriteSupport)
                                {
                                    _callStack = _callStack,
                                    _circularReferences = _circularReferences,
                                    _inlinedMethods = _inlinedMethods
                                }.Visit(callee.BodySyntax.Expression);

                            var substitutions = callee.Parameters.ToDictionary(
                                p => p.Name,
                                p => (ExpressionSyntax)IdentifierName(p.Name));

                            var substitutedBody =
                                (ExpressionSyntax)new ParameterSubstitutionRewriter(substitutions)
                                    .Visit(inlinedBody)!
                                    .WithoutTrivia();

                            LambdaExpressionSyntax lambda;
                            if (lambdaParams.Length == 1)
                            {
                                lambda = SimpleLambdaExpression(lambdaParams[0], substitutedBody);
                            }
                            else
                            {
                                lambda = ParenthesizedLambdaExpression(substitutedBody)
                                    .WithParameterList(ParameterList(SeparatedList(lambdaParams)));
                            }

                            lambda = lambda.WithArrowToken(lambda.ArrowToken.WithLeadingTrivia(Space).WithTrailingTrivia(Space));
                            var newArgs = SeparatedList([args[0].WithExpression(lambda)]);
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

        if (!TryBuildParameterSubstitutions(node, invokedMethod, args, out var substitutionsMap, out var conditionalAccessExpression))
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
            _inlinedMethods[directCallMethod] = callee2;
            var inlinedBody2 = (ExpressionSyntax)new InliningResolver(callee2.SemanticModel, catalog, forUpdateMethod, rewriteSupport)
            {
                _callStack = _callStack,
                _circularReferences = _circularReferences,
                _inlinedMethods = _inlinedMethods
            }.Visit(callee2.BodySyntax.Expression);

            var substituted = new ParameterSubstitutionRewriter(substitutionsMap)
                .Visit(inlinedBody2)
                .WithoutTrivia();

            if (conditionalAccessExpression)
            {
                substituted = substituted.WithAdditionalAnnotations(new SyntaxAnnotation("AlephMapper.InlinedConditional"));
            }

            return substituted;
        }
        finally
        {
            // Remove method from call stack after inlining
            _callStack.Remove(directCallMethod);
        }
    }

    private bool TryBuildParameterSubstitutions(
        InvocationExpressionSyntax node,
        IMethodSymbol invokedMethod,
        SeparatedSyntaxList<ArgumentSyntax> args,
        out Dictionary<string, ExpressionSyntax> substitutions,
        out bool conditionalAccessExpression)
    {
        substitutions = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        conditionalAccessExpression = false;

        var parameters = invokedMethod.Parameters;
        if (parameters.Length == 0)
        {
            return false;
        }

        var nextParamIndex = 0;

        if (invokedMethod.IsExtensionMethod)
        {
            ExpressionSyntax receiver;
            if (node.Parent is ConditionalAccessExpressionSyntax caExpr)
            {
                conditionalAccessExpression = true;
                if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    receiver = (ExpressionSyntax?)(Visit(memberAccess.Expression) ?? memberAccess.Expression);
                }
                else
                {
                    receiver = rewriteSupport != NullConditionalRewrite.None
                        ? _conditionalAccessExpressionsStack.Peek()
                        : (ExpressionSyntax?)(Visit(caExpr.Expression) ?? caExpr.Expression);
                }
            }
            else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                receiver = (ExpressionSyntax?)(Visit(memberAccess.Expression) ?? memberAccess.Expression);
            }
            else
            {
                return false;
            }

            substitutions[parameters[0].Name] = receiver;
            nextParamIndex = 1;
        }

        foreach (var arg in args)
        {
            IParameterSymbol targetParam;
            if (arg.NameColon != null)
            {
                targetParam = parameters.FirstOrDefault(p => p.Name == arg.NameColon.Name.Identifier.Text);
                if (targetParam == null)
                {
                    return false;
                }
            }
            else
            {
                if (nextParamIndex >= parameters.Length)
                {
                    return false;
                }

                targetParam = parameters[nextParamIndex++];
            }

            var rewrittenArg = (ExpressionSyntax?)(Visit(arg.Expression) ?? arg.Expression);
            substitutions[targetParam.Name] = rewrittenArg;
        }

        // Ensure all parameters are covered (no optional/default handling for now)
        if (substitutions.Count != parameters.Length)
        {
            return false;
        }

        return true;
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

