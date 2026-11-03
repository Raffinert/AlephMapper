#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;

namespace AlephMapper.Generation;

/// <summary>C# overload identity; nullable reference annotations do not distinguish overloads.</summary>
internal sealed class MethodSignature : IEquatable<MethodSignature>
{
    private readonly (ITypeSymbol Type, bool ByRef)[] parameters;
    private readonly int arity;
    public string Name { get; }

    private MethodSignature(string name, IEnumerable<(ITypeSymbol Type, bool ByRef)> parameters, int arity)
    {
        Name = name;
        this.parameters = parameters.ToArray();
        this.arity = arity;
    }

    public static MethodSignature Build(string name, IEnumerable<ITypeSymbol> types, int typeParameterCount = 0) =>
        new(name, types.Select(type => (type, false)), typeParameterCount);

    public static MethodSignature FromMethod(IMethodSymbol method) =>
        new(method.Name, method.Parameters.Select(p => (p.Type, p.RefKind != RefKind.None)), method.Arity);

    public bool Equals(MethodSignature? other) => other is not null && Name == other.Name && arity == other.arity &&
        parameters.Length == other.parameters.Length && parameters.Zip(other.parameters, (a, b) =>
            a.ByRef == b.ByRef && SameType(a.Type, b.Type)).All(equal => equal);

    private static ITypeSymbol Normalize(ITypeSymbol type) => type is INamedTypeSymbol { IsTupleType: true } tuple
        ? tuple.TupleUnderlyingType ?? tuple : type;

    private static bool SameType(ITypeSymbol left, ITypeSymbol right)
    {
        left = Normalize(left);
        right = Normalize(right);
        if (SymbolEqualityComparer.Default.Equals(left, right)) return true;
        if ((left.TypeKind == TypeKind.Dynamic || left.SpecialType == SpecialType.System_Object) &&
            (right.TypeKind == TypeKind.Dynamic || right.SpecialType == SpecialType.System_Object)) return true;
        if (left is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } a &&
            right is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } b) return a.Ordinal == b.Ordinal;
        if (left is IArrayTypeSymbol x && right is IArrayTypeSymbol y)
            return x.Rank == y.Rank && x.IsSZArray == y.IsSZArray && SameType(x.ElementType, y.ElementType);
        if (left is IPointerTypeSymbol p && right is IPointerTypeSymbol q) return SameType(p.PointedAtType, q.PointedAtType);
        if (left is INamedTypeSymbol l && right is INamedTypeSymbol r)
            return SymbolEqualityComparer.Default.Equals(l.OriginalDefinition, r.OriginalDefinition) &&
                (l.ContainingType is null ? r.ContainingType is null : r.ContainingType is not null && SameType(l.ContainingType, r.ContainingType)) &&
                l.TypeArguments.Length == r.TypeArguments.Length && l.TypeArguments.Zip(r.TypeArguments, SameType).All(equal => equal);
        return false;
    }

    private static int TypeHash(ITypeSymbol type)
    {
        type = Normalize(type);
        if (type.TypeKind == TypeKind.Dynamic || type.SpecialType == SpecialType.System_Object) return (int)SpecialType.System_Object;
        if (type is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } parameter) return parameter.Ordinal;
        if (type is IArrayTypeSymbol array) return Combine(Combine(array.Rank, array.IsSZArray ? 1 : 0), TypeHash(array.ElementType));
        if (type is IPointerTypeSymbol pointer) return TypeHash(pointer.PointedAtType);
        if (type is INamedTypeSymbol named)
        {
            var hash = SymbolEqualityComparer.Default.GetHashCode(named.OriginalDefinition);
            if (named.ContainingType is not null) hash = Combine(hash, TypeHash(named.ContainingType));
            foreach (var argument in named.TypeArguments) hash = Combine(hash, TypeHash(argument));
            return hash;
        }
        return SymbolEqualityComparer.Default.GetHashCode(type);
    }

    private static int Combine(int a, int b) => unchecked(a * 397 ^ b);
    public override bool Equals(object? obj) => obj is MethodSignature signature && Equals(signature);
    public override int GetHashCode()
    {
        var hash = Combine(StringComparer.Ordinal.GetHashCode(Name), arity);
        foreach (var parameter in parameters) hash = Combine(hash, Combine(TypeHash(parameter.Type), parameter.ByRef ? 1 : 0));
        return hash;
    }
    public override string ToString() => Name + (arity == 0 ? "" : "`" + arity) + "(" + string.Join(",", parameters.Select(p =>
        (p.ByRef ? "ref " : "") + TypeDisplay.ForSymbol(p.Type, NullableAnnotation.None, NullablePolicy.Disabled))) + ")";
}
