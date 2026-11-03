#nullable enable

using System.Collections.Generic;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    private bool TryExpandBody(MappingAnalysis mapping,
        IReadOnlyDictionary<IParameterSymbol, ExpressionSyntax>? substitutions,
        out ExpressionSyntax body, bool extensionExpansion = false)
    {
        var method = SymbolHelpers.Normalize(mapping.MethodSymbol);
        body = mapping.BodySyntax.Expression;
        if (IsCircularReference(method))
        {
            RecordCircularReference(method);
            return false;
        }
        _context.CallStack.Add(method);
        try
        {
            _context.InlinedMethods[method] = mapping;
            var child = new InliningResolver(mapping.SemanticModel, catalog, forUpdateMethod, rewriteSupport,
                nullablePolicy, mapping.ReturnType, substitutions, _context)
            {
                _extensionExpansion = _extensionExpansion || extensionExpansion
            };
            body = (ExpressionSyntax)child.Visit(mapping.BodySyntax.Expression)!;
            return true;
        }
        finally { _context.CallStack.Remove(method); }
    }

    private ExpressionSyntax PreserveReturnConversion(MappingAnalysis mapping, ExpressionSyntax body, bool includeUntyped = false)
    {
        body = body.WithoutTrivia();
        var info = mapping.SemanticModel.GetTypeInfo(mapping.BodySyntax.Expression);
        if ((info.Type is not null || includeUntyped) &&
            SymbolEqualityComparer.Default.Equals(info.ConvertedType, mapping.ReturnType) &&
            !SymbolEqualityComparer.Default.Equals(info.Type, mapping.ReturnType))
            body = CastExpression(ParseTypeName(TypeDisplay.ForSymbol(mapping.ReturnType,
                mapping.ReturnType.NullableAnnotation, nullablePolicy)), ParenthesizedExpression(body));
        return body;
    }
}
