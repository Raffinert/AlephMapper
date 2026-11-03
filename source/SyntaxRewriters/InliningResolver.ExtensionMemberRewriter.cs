#nullable enable

using System.Collections.Generic;
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
        if (!TryBindCallArguments(node, method, mapping, out var substitutions, out _))
            return UnsupportedExtension(node, method);
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
        return new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default)
        {
            [mapping.ReceiverSymbol] = BindReceiver(receiver, alreadyVisited, mapping.ReceiverSymbol.Type)
        };
    }

    private SyntaxNode UnsupportedExtension(SyntaxNode original, ISymbol member)
    {
        _context.UnsupportedExtensions.Add((member, MappingAnalysisFactory.ExtensionUnsupportedReason(member) ?? "the receiver or arguments could not be bound to a source expression body"));
        return original;
    }

    private SyntaxNode ExpandExtension(SyntaxNode original, MappingAnalysis mapping,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax> substitutions)
    {
        if (!TryExpandBody(mapping, substitutions, out var body, extensionExpansion: true)) return original;
        body = PreserveReturnConversion(mapping, body, includeUntyped: true);
        return ParenthesizedExpression(body).WithTriviaFrom(original);
    }
}
