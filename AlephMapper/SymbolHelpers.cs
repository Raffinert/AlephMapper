using System.Collections.Generic;
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
            if (x == y) return true;
            if (x == null || y == null) return false;
            return _base.Equals(Normalize(x), Normalize(y));
        }
        public int GetHashCode(IMethodSymbol obj)
        {
            return _base.GetHashCode(Normalize(obj));
        }
        public static readonly MethodComparer Instance = new MethodComparer();
    }
}