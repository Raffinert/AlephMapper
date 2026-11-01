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
    // Only the currently visited switch arm owns these locals. Keep the original
    // Roslyn symbols, not their identifier text, to avoid collisions with callers.
    private readonly Stack<(ISymbol Symbol, ExpressionSyntax Replacement)> _switchPatternVariables = new();
    public IEnumerable<string> UsingDirectives => _inlinedMethods.SelectMany(il => il.Value.UsingDirectives).Distinct();

    public IReadOnlyList<CircularReferenceInfo> CircularReferences => _circularReferences;
    public IReadOnlyList<UnsafeConditionalReceiverInfo> UnsafeConditionalReceivers => _unsafeConditionalReceivers;
    public IReadOnlyList<UnsupportedNullConditionalInfo> UnsupportedNullConditionals => _unsupportedNullConditionals;
    public IReadOnlyList<UnsupportedSwitchPatternInfo> UnsupportedSwitchPatterns => _unsupportedSwitchPatterns;

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
        if (!CanQuerySemanticModel(node))
            return base.VisitIdentifierName(node);

        var symbol = model.GetSymbolInfo(node).Symbol;
        foreach (var patternVariable in _switchPatternVariables)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, patternVariable.Symbol))
                return patternVariable.Replacement.WithTriviaFrom(node);
        }

        if (parameterSubstitutions is not null &&
            symbol is IParameterSymbol parameter &&
            parameterSubstitutions.TryGetValue(parameter, out var replacement))
        {
            // Parenthesize non-primary expressions to preserve operator precedence after inlining.
            var value = replacement.WithoutTrivia();
            if (value is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or
                ElementAccessExpressionSyntax or InvocationExpressionSyntax or
                LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax or
                ParenthesizedExpressionSyntax))
                value = ParenthesizedExpression(value);
            return value.WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    // Invocation syntax behaves as a primary expression. Inserting a helper
    // body may lower its precedence; group only when the containing operator
    // would otherwise bind part of the helper body. This keeps prior generated
    // snapshots stable where parentheses are unnecessary.
    private static bool NeedsGroupingAtInlineSite(InvocationExpressionSyntax call, ExpressionSyntax body)
    {
        if (body is ParenthesizedExpressionSyntax)
            return false;

        if (call.Parent is BinaryExpressionSyntax parent)
        {
            if (body is not BinaryExpressionSyntax binary)
                return body is ConditionalExpressionSyntax or AssignmentExpressionSyntax or
                    IsPatternExpressionSyntax or SwitchExpressionSyntax;

            var bodyPrecedence = BinaryPrecedence(binary.Kind());
            var parentPrecedence = BinaryPrecedence(parent.Kind());
            // Right-associative ?? requires grouping on the LEFT; other binary
            // operators need grouping on the RIGHT when precedence is equal.
            return bodyPrecedence < parentPrecedence ||
                   bodyPrecedence == parentPrecedence &&
                   (parent.IsKind(SyntaxKind.CoalesceExpression)
                       ? parent.Left == call
                       : parent.Right == call);
        }

        // A cast or unary operator binds more tightly than any binary/ternary
        // expression. In a conditional test, a nested conditional or assignment
        // must likewise be kept intact. Never rely on the pretty printer to
        // recover lost parentheses from the rewritten syntax tree.
        var composite = body is BinaryExpressionSyntax or ConditionalExpressionSyntax or
            AssignmentExpressionSyntax or IsPatternExpressionSyntax or SwitchExpressionSyntax;
        if (!composite)
            return false;

        return call.Parent switch
        {
            CastExpressionSyntax or PrefixUnaryExpressionSyntax or
                PostfixUnaryExpressionSyntax or MemberAccessExpressionSyntax or
                ElementAccessExpressionSyntax or IsPatternExpressionSyntax => true,
            ConditionalExpressionSyntax conditional when conditional.Condition == call =>
                body is ConditionalExpressionSyntax or AssignmentExpressionSyntax,
            _ => false
        };
    }

    private static int BinaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or
            SyntaxKind.ModuloExpression => 13,
        SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => 12,
        SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression or
            SyntaxKind.UnsignedRightShiftExpression => 11,
        SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or
            SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.IsExpression or SyntaxKind.AsExpression => 10,
        SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => 9,
        SyntaxKind.BitwiseAndExpression => 8,
        SyntaxKind.ExclusiveOrExpression => 7,
        SyntaxKind.BitwiseOrExpression => 6,
        SyntaxKind.LogicalAndExpression => 5,
        SyntaxKind.LogicalOrExpression => 4,
        SyntaxKind.CoalesceExpression => 3,
        _ => 0
    };

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

                        // Rewriting the receiver is not recursion into the method-group
                        // callee. Do it before pushing the callee on the call stack,
                        // so Select(Normalize).Select(Normalize) is accepted.
                        var rewrittenReceiver = (ExpressionSyntax)Visit(node.Expression)!;

                        // Only the lambda body is an expansion of this callee.
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
                                    _unsupportedSwitchPatterns = _unsupportedSwitchPatterns
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
                            return node.WithExpression(rewrittenReceiver)
                                .WithArgumentList(node.ArgumentList.WithArguments(newArgs));
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
                _unsupportedSwitchPatterns = _unsupportedSwitchPatterns
            }.Visit(callee2.BodySyntax.Expression)!;

            // Keep existing generated formatting unless grouping is needed for
            // operator precedence (e.g. AddOne(x) * 2 must become (x + 1) * 2).
            var substituted = inlinedBody2.WithoutTrivia();

            // Inlining removes the callee's return boundary. That boundary can
            // perform an implicit conversion (e.g. int -> long) which changes the
            // type of the surrounding arithmetic. Preserve the conversion before
            // embedding the helper body into its caller.
            var returnConversion = callee2.SemanticModel.GetTypeInfo(callee2.BodySyntax.Expression);
            if (returnConversion.Type is { } expressionType &&
                returnConversion.ConvertedType is { } convertedType &&
                !SymbolEqualityComparer.Default.Equals(expressionType, convertedType) &&
                SymbolEqualityComparer.Default.Equals(convertedType, callee2.ReturnType))
            {
                var returnType = ParseTypeName(TypeDisplay.ForSymbol(
                    callee2.ReturnType, callee2.ReturnType.NullableAnnotation, nullablePolicy));
                substituted = CastExpression(returnType, ParenthesizedExpression(substituted));
            }

            if (NeedsGroupingAtInlineSite(node, substituted))
                substituted = ParenthesizedExpression(substituted);

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
