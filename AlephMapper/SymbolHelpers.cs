using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AlephMapper;

internal static class SymbolHelpers
{
    public static IMethodSymbol Normalize(IMethodSymbol m)
    {
        var reduced = m.ReducedFrom;
        if (reduced != null) return reduced;
        var constructed = m.ConstructedFrom;
        if (constructed != null) return constructed;
        return m.OriginalDefinition;
    }

    public sealed class MethodComparer : IEqualityComparer<IMethodSymbol>
    {
        private readonly SymbolEqualityComparer _base = SymbolEqualityComparer.Default;
        public bool Equals(IMethodSymbol x, IMethodSymbol y)
        {
            if (_base.Equals(x, y)) return true;
            if (x == null || y == null) return false;
            return _base.Equals(Normalize(x), Normalize(y));
        }
        public int GetHashCode(IMethodSymbol obj)
        {
            return _base.GetHashCode(Normalize(obj));
        }
        public static readonly MethodComparer Instance = new MethodComparer();
    }

    public static bool HasAttribute( ISymbol sym, string fullName)
    {
        var attribute = GetAttribute(sym, fullName);

        return attribute != null;
    }

    private static AttributeData GetAttribute(ISymbol sym, string fullName)
    {
        var shortName = fullName.Substring(fullName.LastIndexOf('.') + 1);

        var attribute = sym.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == fullName || a.AttributeClass?.Name == shortName);

        return attribute;
    }

    public static object GetAttributeArgumentValue(ISymbol sym, string attributeName, string argumentName)
    {
        var attribute = GetAttribute(sym, attributeName);

        var attributeValue = attribute?.NamedArguments
            .Where(arg => arg.Key == argumentName)
            .Select(arg => arg.Value.Value)
            .FirstOrDefault();

        return attributeValue;
    }

    /// <summary>
    /// Determines whether a type can be compared to null (i.e., can have null values).
    /// Value types (except nullable value types) cannot be null.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type can be null, false otherwise</returns>
    public static bool CanBeNull(ITypeSymbol type)
    {
        if (type == null) return true; // Conservative default

        // Reference types can be null
        if (type.IsReferenceType) return true;

        // Check if it's a nullable value type (e.g., int?, DateTime?)
        if (type.IsValueType && type is INamedTypeSymbol named &&
            named.IsGenericType &&
            named.ConstructedFrom?.ToDisplayString() == "System.Nullable<T>")
        {
            return true;
        }

        // Regular value types (int, DateTime, struct, etc.) cannot be null
        return false;
    }
}