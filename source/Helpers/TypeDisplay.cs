using Microsoft.CodeAnalysis;
using System;

namespace AlephMapper.Helpers;

/// <summary>
/// Provides consistent type display strings that respect the active nullable context.
/// </summary>
internal static class TypeDisplay
{
    private static readonly SymbolDisplayFormat NullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat NonNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static string ForSymbol(ITypeSymbol symbol, SemanticModel model, int position)
        => ForSymbol(symbol, symbol.NullableAnnotation, NullablePolicy.From(model, position));

    public static string ForSymbol(ITypeSymbol symbol, NullableAnnotation annotationOverride, NullablePolicy nullablePolicy)
    {
        var format = nullablePolicy.AnnotationsEnabled ? NullableFormat : NonNullableFormat;
        var effectiveAnnotation = annotationOverride != NullableAnnotation.None
            ? annotationOverride
            : symbol.NullableAnnotation;
        var displaySymbol = annotationOverride != NullableAnnotation.None
            ? symbol.WithNullableAnnotation(annotationOverride)
            : symbol;
        var display = displaySymbol.ToDisplayString(format);

        if (nullablePolicy.AnnotationsEnabled &&
            effectiveAnnotation == NullableAnnotation.Annotated &&
            !display.EndsWith("?", StringComparison.Ordinal))
        {
            display += "?";
        }

        return display;
    }
}
