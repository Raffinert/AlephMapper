#nullable enable

using System.Collections.Generic;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    private ExpressionSyntax PreserveArgumentConversion(ExpressionSyntax? original, ExpressionSyntax rewritten,
        ITypeSymbol? expectedType = null)
    {
        if (original is null || !CanQuerySemanticModel(original)) return rewritten;
        var info = model.GetTypeInfo(original);
        var converted = expectedType ?? info.ConvertedType;
        if (converted is null || SymbolEqualityComparer.Default.Equals(info.Type, converted)) return rewritten;
        return ParenthesizedExpression(CastExpression(ParseTypeName(TypeDisplay.ForSymbol(
            converted, converted.NullableAnnotation, nullablePolicy)), ParenthesizedExpression(rewritten)));
    }

    private bool TryBindCallArguments(InvocationExpressionSyntax node, IMethodSymbol invokedMethod, MappingAnalysis mapping,
        out Dictionary<IParameterSymbol, ExpressionSyntax> substitutions, out bool conditionalAccess)
    {
        substitutions = new(SymbolEqualityComparer.Default);
        conditionalAccess = node.Parent is ConditionalAccessExpressionSyntax;
        var receiverParameter = mapping.ReceiverSymbol;
        ITypeSymbol? receiverType = receiverParameter?.Type;
        if (receiverParameter is null && invokedMethod.ReducedFrom is not null)
        {
            receiverParameter = mapping.MethodSymbol.Parameters[0];
            receiverType = invokedMethod.ReceiverType ?? receiverParameter.Type;
        }
        if (receiverParameter is not null)
        {
            ExpressionSyntax receiver;
            var alreadyVisited = false;
            ExpressionSyntax? originalReceiver = null;
            if (node.Expression is MemberAccessExpressionSyntax access) receiver = access.Expression;
            else if (node.Expression is MemberBindingExpressionSyntax && _conditionalAccessExpressionsStack.Count > 0)
            {
                receiver = _conditionalAccessExpressionsStack.Peek();
                alreadyVisited = true;
                originalReceiver = (node.Parent as ConditionalAccessExpressionSyntax)?.Expression;
            }
            else return false;
            substitutions[receiverParameter] = BindReceiver(receiver, alreadyVisited, receiverType!, originalReceiver);
        }
        foreach (var argument in node.ArgumentList.Arguments)
        {
            if (!CanQuerySemanticModel(argument) || model.GetOperation(argument) is not IArgumentOperation { Parameter: { } bound })
                return false;
            // Operations normally expose the unreduced static parameter. Account for
            // reduced symbols too; keys always belong to the original callee declaration.
            var ordinal = bound.Ordinal + (bound.ContainingSymbol is IMethodSymbol { ReducedFrom: not null } ? 1 : 0);
            if (ordinal >= mapping.MethodSymbol.Parameters.Length) return false;
            var parameter = mapping.MethodSymbol.Parameters[ordinal];
            if (substitutions.ContainsKey(parameter)) return false;
            substitutions.Add(parameter, PreserveArgumentConversion(argument.Expression, (ExpressionSyntax)Visit(argument.Expression)!));
        }
        // Optional/expanded params are not silently invented during source expansion.
        return substitutions.Count == mapping.Parameters.Count;
    }

    private ExpressionSyntax BindReceiver(ExpressionSyntax receiver, bool alreadyVisited, ITypeSymbol type,
        ExpressionSyntax? originalReceiver = null)
    {
        if (!alreadyVisited)
            return PreserveArgumentConversion(receiver, (ExpressionSyntax)Visit(receiver)!, type);
        // Use the original receiver to decide whether the rewritten receiver needs a conversion.
        // Identity casts obscure root accesses used by adaptation.
        if (originalReceiver is not null && CanQuerySemanticModel(originalReceiver))
            return PreserveArgumentConversion(originalReceiver, receiver, type);
        // Extension property receivers can lack original syntax; retain their declared conversion.
        return ParenthesizedExpression(CastExpression(ParseTypeName(TypeDisplay.ForSymbol(type, type.NullableAnnotation, nullablePolicy)),
            ParenthesizedExpression(receiver)));
    }
}
