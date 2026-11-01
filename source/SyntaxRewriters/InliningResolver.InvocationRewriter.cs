#nullable enable

using AlephMapper.Helpers;
using AlephMapper.Generation;
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
    MappingCatalog catalog,
    bool forUpdateMethod,
    NullConditionalRewrite rewriteSupport,
    NullablePolicy nullablePolicy,
    ITypeSymbol? returnTypeToAnnotate = null,
    IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax>? parameterSubstitutions = null)
    : CSharpSyntaxRewriter
{
    internal const string InlinedConditionalAnnotation = "AlephMapper.InlinedConditional";
    internal const string InlinedReturnCreationAnnotation = "AlephMapper.InlinedReturnCreation";

    private HashSet<IMethodSymbol> _callStack = new(SymbolEqualityComparer.Default);
    private List<CircularReferenceInfo> _circularReferences = [];
    private Dictionary<IMethodSymbol, MappingAnalysis> _inlinedMethods = new(SymbolEqualityComparer.Default);
    private List<UnsafeConditionalReceiverInfo> _unsafeConditionalReceivers = [];
    private List<UnsupportedNullConditionalInfo> _unsupportedNullConditionals = [];
    private List<UnsupportedSwitchPatternInfo> _unsupportedSwitchPatterns = [];
    private List<UnsafeRepeatedArgumentInfo> _unsafeRepeatedArguments = [];
    public IEnumerable<string> UsingDirectives => _inlinedMethods.SelectMany(il => il.Value.UsingDirectives).Distinct();

    public IReadOnlyList<CircularReferenceInfo> CircularReferences => _circularReferences;
    public IReadOnlyList<UnsafeConditionalReceiverInfo> UnsafeConditionalReceivers => _unsafeConditionalReceivers;
    public IReadOnlyList<UnsupportedNullConditionalInfo> UnsupportedNullConditionals => _unsupportedNullConditionals;
    public IReadOnlyList<UnsupportedSwitchPatternInfo> UnsupportedSwitchPatterns => _unsupportedSwitchPatterns;
    public IReadOnlyList<UnsafeRepeatedArgumentInfo> UnsafeRepeatedArguments => _unsafeRepeatedArguments;

    private IMethodSymbol? ResolveMethodGroupSymbol(ExpressionSyntax expr)
    {
        if (!CanQuerySemanticModel(expr))
        {
            return null;
        }

        var si = model.GetSymbolInfo(expr);
        if (si.Symbol is IMethodSymbol ms) return ms;
        return null;
    }

    private bool CanQuerySemanticModel(SyntaxNode node)
    {
        return node.SyntaxTree == model.SyntaxTree;
    }

    private static IMethodSymbol? TryGetDelegateInvoke(IMethodSymbol invokedMethod, int argIndex)
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

    // Bind substitutions to Roslyn parameter symbols, not identifier text. In particular,
    // 'person.Name' and a parameter named 'Name' must never be treated as the same symbol.
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (parameterSubstitutions is not null &&
            CanQuerySemanticModel(node) &&
            model.GetSymbolInfo(node).Symbol is IParameterSymbol parameter &&
            parameterSubstitutions.TryGetValue(parameter, out var replacement))
        {
            // Parenthesize non-primary expressions to preserve operator precedence after inlining.
            var value = replacement.WithoutTrivia();
            if (value is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or
                ElementAccessExpressionSyntax or InvocationExpressionSyntax or
                LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax))
                value = ParenthesizedExpression(value);
            return value.WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    private static bool CouldEvaluateWithSideEffects(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().Any(n =>
            n is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or
                AssignmentExpressionSyntax or AwaitExpressionSyntax or
                PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax);

    private static int CountParameterReferences(MappingAnalysis callee, IParameterSymbol parameter) =>
        callee.BodySyntax.Expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Count(node => callee.SemanticModel.GetSymbolInfo(node).Symbol is IParameterSymbol referenced &&
                SymbolEqualityComparer.Default.Equals(referenced, parameter));

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Parent == null ||
            !CanQuerySemanticModel(node) ||
            model.GetSymbolInfo(node.Expression).Symbol is not IMethodSymbol invokedMethod)
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
                                (ExpressionSyntax)new InliningResolver(callee.SemanticModel, catalog, forUpdateMethod, rewriteSupport, nullablePolicy, callee.ReturnType)
                                {
                                    _callStack = _callStack,
                                    _circularReferences = _circularReferences,
                                    _inlinedMethods = _inlinedMethods,
                                    _unsafeConditionalReceivers = _unsafeConditionalReceivers,
                                    _unsupportedNullConditionals = _unsupportedNullConditionals,
                                    _unsupportedSwitchPatterns = _unsupportedSwitchPatterns,
                                    _unsafeRepeatedArguments = _unsafeRepeatedArguments
                                }.Visit(callee.BodySyntax.Expression)!;

                            // Lambda parameters intentionally keep the callee's parameter names.
                            var substitutedBody = inlinedBody.WithoutTrivia();

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

        // NullConditionalRewrite.None must leave the original conditional call intact.
        // Inlining it would detach the body from the ?. operator and change null semantics.
        if (isConditionalAccess && rewriteSupport == NullConditionalRewrite.None)
        {
            return node.WithArgumentList((ArgumentListSyntax)Visit(node.ArgumentList)!);
        }

        if (!TryBuildParameterSubstitutions(node, invokedMethod, callee2.MethodSymbol, args, out var substitutionsMap, out var conditionalAccessExpression))
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

        // Inlining may duplicate a call or object creation argument that the original
        // method evaluates just once. Refuse the unsafe projection rather than silently
        // changing behavior. Ordinary parameters/constants remain safely reusable.
        if (substitutionsMap.Any(entry =>
            CouldEvaluateWithSideEffects(entry.Value) &&
            CountParameterReferences(callee2, entry.Key) > 1))
        {
            _unsafeRepeatedArguments.Add(new UnsafeRepeatedArgumentInfo(node));
            return base.VisitInvocationExpression(node);
        }

        // Add method to call stack before inlining
        _callStack.Add(directCallMethod);
        try
        {
            _inlinedMethods[directCallMethod] = callee2;
            var inlinedBody2 = (ExpressionSyntax)new InliningResolver(callee2.SemanticModel, catalog, forUpdateMethod, rewriteSupport, nullablePolicy, callee2.ReturnType, substitutionsMap)
            {
                _callStack = _callStack,
                _circularReferences = _circularReferences,
                _inlinedMethods = _inlinedMethods,
                _unsafeConditionalReceivers = _unsafeConditionalReceivers,
                _unsupportedNullConditionals = _unsupportedNullConditionals,
                _unsupportedSwitchPatterns = _unsupportedSwitchPatterns,
                _unsafeRepeatedArguments = _unsafeRepeatedArguments
            }.Visit(callee2.BodySyntax.Expression)!;

            var substituted = inlinedBody2.WithoutTrivia();

            if (conditionalAccessExpression)
            {
                substituted = substituted.WithAdditionalAnnotations(new SyntaxAnnotation(InlinedConditionalAnnotation));
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
        IMethodSymbol calleeMethod,
        SeparatedSyntaxList<ArgumentSyntax> args,
        out Dictionary<IParameterSymbol, ExpressionSyntax> substitutions,
        out bool conditionalAccessExpression)
    {
        substitutions = new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);
        conditionalAccessExpression = false;

        // When an extension method is called via dot syntax (e.g. address.ToDto()),
        // Roslyn returns the "reduced" form where Parameters does NOT include the 'this' parameter.
        // We need the original definition's parameters (which include 'this') for correct substitution.
        var isReducedExtension = invokedMethod.IsExtensionMethod && invokedMethod.ReducedFrom != null;
        // Keys must refer to symbols in the callee's source syntax tree.
        var parameters = calleeMethod.Parameters;

        if (parameters.Length == 0)
        {
            return false;
        }

        var nextParamIndex = 0;

        if (isReducedExtension)
        {
            ExpressionSyntax receiver;
            if (node.Parent is ConditionalAccessExpressionSyntax caExpr)
            {
                conditionalAccessExpression = true;
                if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    receiver = (ExpressionSyntax)(Visit(memberAccess.Expression) ?? memberAccess.Expression);
                }
                else
                {
                    receiver = rewriteSupport != NullConditionalRewrite.None
                        ? _conditionalAccessExpressionsStack.Peek()!
                        : (ExpressionSyntax)(Visit(caExpr.Expression) ?? caExpr.Expression);
                }
            }
            else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                receiver = (ExpressionSyntax)(Visit(memberAccess.Expression) ?? memberAccess.Expression);
            }
            else
            {
                return false;
            }

            substitutions[parameters[0]] = receiver;
            nextParamIndex = 1;
        }

        foreach (var arg in args)
        {
            IParameterSymbol? targetParam;
            if (arg.NameColon != null)
            {
                targetParam = parameters.FirstOrDefault(p => p.Name == arg.NameColon.Name.Identifier.Text);
            }
            else
            {
                if (nextParamIndex >= parameters.Length)
                {
                    return false;
                }

                targetParam = parameters[nextParamIndex++];
            }

            if (targetParam == null)
            {
                return false;
            }

            var rewrittenArg = (ExpressionSyntax)(Visit(arg.Expression) ?? arg.Expression);
            substitutions[targetParam] = rewrittenArg;
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

internal sealed class UnsafeRepeatedArgumentInfo(InvocationExpressionSyntax invocation)
{
    public Location Location { get; } = invocation.GetLocation();
    public string Text { get; } = invocation.ToString();
}

internal sealed class UnsafeConditionalReceiverInfo(ExpressionSyntax expression)
{
    public ExpressionSyntax Expression { get; } = expression;
    public Location Location { get; } = expression.GetLocation();
}

internal sealed class UnsupportedNullConditionalInfo(ConditionalAccessExpressionSyntax expression)
{
    public ConditionalAccessExpressionSyntax Expression { get; } = expression;
    public Location Location { get; } = expression.GetLocation();
}

#nullable restore
