using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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

internal sealed class InliningResolver(SemanticModel model, IDictionary<IMethodSymbol, MappingModel> catalog, bool forUpdateMethod)
    : CSharpSyntaxRewriter
{
    private HashSet<IMethodSymbol> _callStack = new(SymbolEqualityComparer.Default);
    private List<CircularReferenceInfo> _circularReferences = [];
    private HashSet<ITypeSymbol> _inlinedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

    public IReadOnlyList<CircularReferenceInfo> CircularReferences => _circularReferences;
    public HashSet<ITypeSymbol> InlinedTypes => _inlinedTypes;

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

        string type = model.GetTypeInfo(implicitNew).Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (type == null)
        {
            return base.VisitImplicitObjectCreationExpression(implicitNew);
        }

        return ObjectCreationExpression(IdentifierName(type))
                    .WithInitializer((InitializerExpressionSyntax)VisitInitializerExpression(implicitNew.Initializer!))
                    .WithArgumentList((ArgumentListSyntax)VisitArgumentList(implicitNew.ArgumentList))
                    .WithNewKeyword(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
    }

    public override SyntaxNode VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        if (forUpdateMethod)
        {
            // fix missing leading trivia on 'switch' keyword
            var switchExpression = (SwitchExpressionSyntax)base.VisitSwitchExpression(node);
            return switchExpression?.WithSwitchKeyword(node.SwitchKeyword.WithLeadingTrivia(Space));
        }

        // Reverse arms order to start from the default value
        var arms = node.Arms.Reverse();

        ExpressionSyntax currentExpression = null;

        foreach (var arm in arms)
        {
            var armExpression = (ExpressionSyntax)Visit(arm.Expression.WithoutTrivia());

            // Handle fallback value
            if (currentExpression == null)
            {
                currentExpression = arm.Pattern is DiscardPatternSyntax
                    ? armExpression
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                continue;
            }

            // Handle each arm, only if it's a constant expression
            if (arm.Pattern is ConstantPatternSyntax constant)
            {
                ExpressionSyntax expression = BinaryExpression(SyntaxKind.EqualsExpression, (ExpressionSyntax)Visit(node.GoverningExpression.WithoutTrivia()), constant.Expression.WithoutTrivia());

                // Add the when clause as a AND expression
                if (arm.WhenClause != null)
                {
                    expression = BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        expression,
                        (ExpressionSyntax)Visit(arm.WhenClause.Condition.WithoutTrivia())
                    );
                }

                currentExpression = ConditionalExpression(
                    expression,
                    armExpression,
                    currentExpression
                );

                continue;
            }

            if (arm.Pattern is DeclarationPatternSyntax declaration)
            {
                var getTypeExpression = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    (ExpressionSyntax)Visit(node.GoverningExpression.WithoutTrivia()),
                    IdentifierName("GetType")
                );

                var getTypeCall = InvocationExpression(getTypeExpression);
                var typeofExpression = TypeOfExpression(declaration.Type);
                var equalsExpression = BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    getTypeCall,
                    typeofExpression
                );

                ExpressionSyntax condition = equalsExpression;
                if (arm.WhenClause != null)
                {
                    condition = BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        equalsExpression,
                        (ExpressionSyntax)Visit(arm.WhenClause.Condition.WithoutTrivia())
                    );
                }

                var modifiedArmExpression = ReplaceVariableWithCast(armExpression, declaration, node.GoverningExpression.WithoutTrivia());
                currentExpression = ConditionalExpression(
                    condition,
                    modifiedArmExpression,
                    currentExpression
                );

                continue;
            }

            throw new InvalidOperationException(
                $"Switch expressions rewriting supports only constant values and declaration patterns (Type var). " +
                $"Unsupported pattern: {arm.Pattern.GetType().Name}"
            );
        }

        return currentExpression;
    }

    private ExpressionSyntax ReplaceVariableWithCast(ExpressionSyntax expression, DeclarationPatternSyntax declaration, ExpressionSyntax governingExpression)
    {
        if (declaration.Designation is SingleVariableDesignationSyntax variableDesignation)
        {
            var variableName = variableDesignation.Identifier.ValueText;

            var castExpression = ParenthesizedExpression(
                CastExpression(
                    declaration.Type,
                    (ExpressionSyntax)Visit(governingExpression)
                )
            );

            var rewriter = new ParameterSubstitutionRewriter(variableName, castExpression);
            return (ExpressionSyntax)rewriter.Visit(expression);
        }

        return expression;
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
                            _inlinedTypes.Add(callee.ReturnType);
                            var inlinedBody = (ExpressionSyntax)new InliningResolver(callee.SemanticModel, catalog, forUpdateMethod)
                            {
                                _callStack = _callStack, 
                                _circularReferences = _circularReferences, 
                                _inlinedTypes = _inlinedTypes
                            }.Visit(callee.BodySyntax.Expression);

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
            _inlinedTypes.Add(callee2.ReturnType);
            var inlinedBody2 = (ExpressionSyntax)new InliningResolver(callee2.SemanticModel, catalog, forUpdateMethod)
            {
                _callStack = _callStack, 
                _circularReferences = _circularReferences,
                _inlinedTypes = _inlinedTypes
            }.Visit(callee2.BodySyntax.Expression);

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