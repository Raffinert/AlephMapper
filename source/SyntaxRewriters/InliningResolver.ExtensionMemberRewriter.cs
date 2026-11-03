#nullable enable
using System.Collections.Generic;
using System.Linq;
using AlephMapper.Generation;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    private SyntaxNode RewriteExtensionCall(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        if (node.Parent is ConditionalAccessExpressionSyntax && rewriteSupport == NullConditionalRewrite.None)
            return node;
        if (!catalog.TryGetValue(method, out var mapping)) return UnsupportedExtension(node, method);
        ExpressionSyntax? receiver = node.Expression is MemberAccessExpressionSyntax access ? access.Expression : null;
        var alreadyVisited = false;
        if (node.Expression is MemberBindingExpressionSyntax && _conditionalAccessExpressionsStack.Count > 0)
        {
            receiver = _conditionalAccessExpressionsStack.Peek();
            alreadyVisited = true;
        }
        if (receiver is null) return UnsupportedExtension(node, method);
        var substitutions = BindExtensionReceiver(mapping, receiver, alreadyVisited);
        var next = 0;
        foreach (var argument in node.ArgumentList.Arguments)
        {
            var parameter = argument.NameColon is { } name
                ? mapping.MethodSymbol.Parameters.FirstOrDefault(p => p.Name == name.Name.Identifier.ValueText)
                : next < mapping.MethodSymbol.Parameters.Length ? mapping.MethodSymbol.Parameters[next++] : null;
            if (parameter is null) return UnsupportedExtension(node, method);
            substitutions[parameter] = PreserveArgumentConversion(argument.Expression, (ExpressionSyntax)Visit(argument.Expression)!);
        }
        if (substitutions.Count != mapping.Parameters.Count) return UnsupportedExtension(node, method);
        return ExpandExtension(node, mapping, substitutions);
    }

    private SyntaxNode RewriteExtensionProperty(ExpressionSyntax node, IPropertySymbol property,
        ExpressionSyntax receiver, bool receiverAlreadyVisited = false)
    {
        if (!catalog.TryGetProperty(property, out var mapping)) return UnsupportedExtension(node, property);
        return ExpandExtension(node, mapping, BindExtensionReceiver(mapping, receiver, receiverAlreadyVisited));
    }

    private Dictionary<IParameterSymbol, ExpressionSyntax> BindExtensionReceiver(MappingAnalysis mapping,
        ExpressionSyntax receiver, bool alreadyVisited)
    {
        var rewritten = alreadyVisited ? receiver : (ExpressionSyntax)Visit(receiver)!;
        return new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default)
        {
            [mapping.ReceiverSymbol] = PreserveArgumentConversion(alreadyVisited ? null : receiver, rewritten, mapping.ReceiverSymbol.Type)
        };
    }

    private SyntaxNode UnsupportedExtension(SyntaxNode original, ISymbol member)
    {
        _unsupportedExtensions.Add((member, MappingAnalysisFactory.ExtensionUnsupportedReason(member) ?? "the receiver or arguments could not be bound to a source expression body"));
        return original;
    }

    private SyntaxNode ExpandExtension(SyntaxNode original, MappingAnalysis mapping,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> substitutions)
    {
        var token = SymbolHelpers.Normalize(mapping.MethodSymbol);
        if (IsCircularReference(token))
        {
            RecordCircularReference(token);
            return original;
        }
        _callStack.Add(token);
        try
        {
            _inlinedMethods[token] = mapping;
            var resolver = new InliningResolver(mapping.SemanticModel, catalog, forUpdateMethod, rewriteSupport,
                nullablePolicy, mapping.ReturnType, substitutions)
            {
                _callStack = _callStack,
                _circularReferences = _circularReferences,
                _inlinedMethods = _inlinedMethods,
                _unsafeConditionalReceivers = _unsafeConditionalReceivers,
                _unsupportedNullConditionals = _unsupportedNullConditionals,
                _unsupportedSwitchPatterns = _unsupportedSwitchPatterns,
                _unsupportedExtensions = _unsupportedExtensions
            };
            var body = (ExpressionSyntax)resolver.Visit(mapping.BodySyntax.Expression)!;
            var info = mapping.SemanticModel.GetTypeInfo(mapping.BodySyntax.Expression);
            if (!SymbolEqualityComparer.Default.Equals(info.Type, mapping.ReturnType))
                body = CastExpression(ParseTypeName(TypeDisplay.ForSymbol(mapping.ReturnType,
                    mapping.ReturnType.NullableAnnotation, nullablePolicy)), ParenthesizedExpression(body));
            // Both methods and properties are primary expressions at the call site.
            return ParenthesizedExpression(body.WithoutTrivia()).WithTriviaFrom(original);
        }
        finally { _callStack.Remove(token); }
    }
}
