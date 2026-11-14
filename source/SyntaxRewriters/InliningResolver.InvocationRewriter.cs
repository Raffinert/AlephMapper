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
    IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax>? parameterSubstitutions = null,
    InliningContext? context = null)
    : CSharpSyntaxRewriter
{
    internal const string InlinedConditionalAnnotation = "AlephMapper.InlinedConditional";
    internal const string InlinedReturnCreationAnnotation = "AlephMapper.InlinedReturnCreation";

    private readonly Stack<(ISymbol Symbol, ExpressionSyntax Replacement)> _lambdaVariables = new();
    private bool _extensionExpansion;
    private readonly InliningContext _context = context ?? new();

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (_context.OutputType is null && node is not null && CanQuerySemanticModel(node))
        {
            var symbol = model.GetEnclosingSymbol(node.SpanStart);
            _context.OutputType = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
            if (_context.OutputType is not null && RoslynCompatibility.IsExtensionType(_context.OutputType)) _context.OutputType = _context.OutputType.ContainingType;
        }
        return base.Visit(node);
    }

    public IReadOnlyList<(ISymbol Member, string Reason)> UnsupportedExtensions => _context.UnsupportedExtensions;
    // Only the currently visited switch arm owns these locals. Keep the original
    // Roslyn symbols, not their identifier text, to avoid collisions with callers.
    private readonly Stack<(ISymbol Symbol, ExpressionSyntax Replacement)> _switchPatternVariables = new();
    public IEnumerable<string> UsingDirectives => _context.InlinedMethods.SelectMany(il => il.Value.UsingDirectives).Distinct();

    public IReadOnlyList<CircularReferenceInfo> CircularReferences => _context.CircularReferences;
    public IReadOnlyList<UnsafeConditionalReceiverInfo> UnsafeConditionalReceivers => _context.UnsafeConditionalReceivers;
    public IReadOnlyList<UnsupportedNullConditionalInfo> UnsupportedNullConditionals => _context.UnsupportedNullConditionals;
    public IReadOnlyList<UnsupportedSwitchPatternInfo> UnsupportedSwitchPatterns => _context.UnsupportedSwitchPatterns;

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
        return _context.CallStack.Contains(method);
    }

    private void RecordCircularReference(IMethodSymbol method)
    {
        var circularRef = new CircularReferenceInfo(method, _context.CallStack.Append(method));
        _context.CircularReferences.Add(circularRef);
    }

    // Bind substitutions to Roslyn parameter symbols, not identifier text. In particular,
    // 'person.Name' and a parameter named 'Name' must never be treated as the same symbol.
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!CanQuerySemanticModel(node))
            return base.VisitIdentifierName(node);

        var symbol = model.GetSymbolInfo(node).Symbol;
        foreach (var variable in _lambdaVariables)
            if (SymbolEqualityComparer.Default.Equals(symbol, variable.Symbol))
                return variable.Replacement.WithTriviaFrom(node);
        if (TryRewriteExpandedName(node, node, symbol, out var expandedReference))
            return expandedReference;
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

        // Generated members from different partial declarations share one file.
        // Bind names in their original semantic model before imports are merged.
        if (!(node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node) &&
            !(node.Parent is QualifiedNameSyntax qualifiedName && qualifiedName.Right == node))
        {
            var alias = model.GetAliasInfo(node);
            var target = alias?.Target ?? symbol;
            if (target is INamedTypeSymbol or INamespaceSymbol)
            {
                var qualified = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return ParseName(qualified).WithTriviaFrom(node);
            }

            if (TryQualifyStaticName(node, node, target, out var staticReference))
                return staticReference;
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
        // Preserve the original constant before qualifying names in the operand.
        // For example, nameof(Int32) must not become the invalid nameof(int).
        if (CanQuerySemanticModel(node) && model.GetOperation(node) is Microsoft.CodeAnalysis.Operations.INameOfOperation &&
            model.GetConstantValue(node) is { HasValue: true, Value: string name })
            return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(name)).WithTriviaFrom(node);
        if (node.Parent == null ||
            !CanQuerySemanticModel(node) ||
            model.GetSymbolInfo(node.Expression).Symbol is not IMethodSymbol invokedMethod)
        {
            return base.VisitInvocationExpression(node);
        }

        if (RoslynCompatibility.IsExtensionType(invokedMethod.ContainingType))
            return RewriteExtensionCall(node, invokedMethod);

        var args = node.ArgumentList.Arguments;
        var firstArgExpression = args.Count > 0 ? args[0].Expression : null;
        var isConditionalAccess = node.Parent is ConditionalAccessExpressionSyntax;

        // Handle method-group arguments first (Select(MapToX))
        if (!isConditionalAccess && firstArgExpression is IdentifierNameSyntax or MemberAccessExpressionSyntax or GenericNameSyntax or QualifiedNameSyntax
            or AliasQualifiedNameSyntax)
        {
            var methodGroup = ResolveMethodGroupSymbol(firstArgExpression);
            if (methodGroup is not null && RoslynCompatibility.IsExtensionType(methodGroup.ContainingType))
            {
                _context.UnsupportedExtensions.Add((methodGroup, "bound extension method groups are not supported; use an explicit expression lambda"));
                return node;
            }

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

                        if (!TryExpandBody(callee, null, out var inlinedBody))
                            return base.VisitInvocationExpression(node);
                        // Lambda parameters intentionally keep the callee's parameter names.
                        var substitutedBody = PreserveReturnConversion(callee, inlinedBody);

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

        if (!TryBindCallArguments(node, invokedMethod, callee2, out var substitutionsMap, out var conditionalAccessExpression))
        {
            return base.VisitInvocationExpression(node)?.WithoutTrivia();
        }

        if (!TryExpandBody(callee2, substitutionsMap, out var inlinedBody2))
            return base.VisitInvocationExpression(node)?.WithoutTrivia();
        var substituted = PreserveReturnConversion(callee2, inlinedBody2);
        if (NeedsGroupingAtInlineSite(node, substituted))
            substituted = ParenthesizedExpression(substituted);
        if (conditionalAccessExpression)
            substituted = substituted.WithAdditionalAnnotations(new SyntaxAnnotation(InlinedConditionalAnnotation));
        return substituted;
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
