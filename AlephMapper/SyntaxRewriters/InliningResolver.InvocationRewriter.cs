using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        if (model.GetSymbolInfo(node.Expression).Symbol is not IMethodSymbol invokedMethod)
        {
            return base.VisitInvocationExpression(node);
        }

        var args = node.ArgumentList.Arguments;

        // Handle extension methods without arguments differently - they show up as static methods with the first parameter being 'this'
        // For extension methods, we need to treat the receiver (left side of the dot) as the first argument
        ExpressionSyntax firstArg;
        bool conditionalAccessExpression = false;

        if (invokedMethod.IsExtensionMethod && args.Count == 0)
        {
            if (node.Parent is ConditionalAccessExpressionSyntax caExpr)
            {
                if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    conditionalAccessExpression = true;
                    firstArg = (ExpressionSyntax)Visit(memberAccess.Expression);
                }
                else
                {
                    conditionalAccessExpression = true;
                    // Properly construct the receiver expression without using ParseExpression.
                    // If we are rewriting null-conditionals, the stack top contains the current target.
                    // Otherwise, use the original conditional-access receiver expression to preserve chaining.
                    firstArg = rewriteSupport != NullConditionalRewrite.None
                        ? _conditionalAccessExpressionsStack.Peek()
                        : (ExpressionSyntax)Visit(caExpr.Expression);
                }
            }
            else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                firstArg = memberAccess.Expression;
            }
            else
            {
                return base.VisitInvocationExpression(node);
            }
        }
        else
        {
            if (args.Count != 1)
            {
                return base.VisitInvocationExpression(node);
            }

            firstArg = args[0].Expression;
        }

        // Handle method-group arguments first (Select(MapToX))
        if (!conditionalAccessExpression && firstArg is IdentifierNameSyntax or MemberAccessExpressionSyntax or GenericNameSyntax or QualifiedNameSyntax
            or AliasQualifiedNameSyntax)
        {
            var methodGroup = ResolveMethodGroupSymbol(firstArg);

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
                            _inlinedMethods[normalizedMethod] = callee;
                            var inlinedBody =
                                (ExpressionSyntax)new InliningResolver(callee.SemanticModel, catalog, forUpdateMethod, rewriteSupport)
                                {
                                    _callStack = _callStack,
                                    _circularReferences = _circularReferences,
                                    _inlinedMethods = _inlinedMethods
                                }.Visit(callee.BodySyntax.Expression);

                            var substitutedBody =
                                (ExpressionSyntax)new ParameterSubstitutionRewriter(callee.ParamName,
                                        IdentifierName(paramName))
                                    .Visit(inlinedBody)!
                                    .WithoutTrivia();

                            var lambda = SimpleLambdaExpression(lambdaParam, substitutedBody);
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

            var substituted = new ParameterSubstitutionRewriter(callee2.ParamName, firstArg)
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
}

/// <summary>
/// Contains information about a circular reference detected during inlining
/// </summary>
internal class CircularReferenceInfo(IMethodSymbol method, IEnumerable<IMethodSymbol> callStack)
{
    public IMethodSymbol Method { get; } = method;
    public string CallChain { get; } = string.Join(" -> ", callStack.Select(m => $"{m.ContainingType.Name}.{m.Name}"));
}

