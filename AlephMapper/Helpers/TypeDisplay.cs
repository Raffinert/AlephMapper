using Microsoft.CodeAnalysis;
using System;

namespace AlephMapper.Helpers;

/// <summary>
/// Provides consistent type display strings that respect the active nullable context.
/// </summary>
internal static class TypeDisplay
{
    private static readonly SymbolDisplayFormat NullableFormat =
        SymbolDisplayFormat.MinimallyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat NonNullableFormat =
        SymbolDisplayFormat.MinimallyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static string ForSymbol(ITypeSymbol symbol, SemanticModel model, int position)
        => ForSymbol(symbol, symbol.NullableAnnotation, model.GetNullableContext(position));

    public static string ForSymbol(ITypeSymbol symbol, NullableAnnotation annotationOverride, NullableContext nullableContext)
    {
        var annotationsEnabled = nullableContext is NullableContext.Enabled
            or NullableContext.AnnotationsEnabled
            or NullableContext.AnnotationsContextInherited;

        var format = annotationsEnabled ? NullableFormat : NonNullableFormat;
        var display = symbol.ToDisplayString(format);

        var effectiveAnnotation = annotationOverride != NullableAnnotation.None
            ? annotationOverride
            : symbol.NullableAnnotation;

        if (annotationsEnabled &&
            effectiveAnnotation == NullableAnnotation.Annotated &&
            !display.EndsWith("?", StringComparison.Ordinal))
        {
            display += "?";
        }

        return display;
    }
}
