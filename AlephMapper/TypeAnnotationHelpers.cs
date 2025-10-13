using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace AlephMapper;

/// <summary>
/// Represents type information for a property path during updateable method generation.
/// This helps determine whether null checks are needed based on the actual type.
/// </summary>
internal class PropertyTypeInfo
{
    public PropertyTypeInfo(string propertyPath, ITypeSymbol type)
    {
        PropertyPath = propertyPath;
        Type = type;
        IsValueType = type.IsValueType;
        IsNullableValueType = type.IsValueType && type.CanBeReferencedByName && 
                              type is INamedTypeSymbol named && 
                              named.IsGenericType && 
                              named.ConstructedFrom?.ToDisplayString() == "System.Nullable<T>";
        IsReferenceType = type.IsReferenceType;
        CanBeNull = IsReferenceType || IsNullableValueType;
        TypeDisplayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public string PropertyPath { get; }
    public ITypeSymbol Type { get; }
    public bool IsValueType { get; }
    public bool IsNullableValueType { get; }
    public bool IsReferenceType { get; }
    public bool CanBeNull { get; }
    public string TypeDisplayName { get; }
}

/// <summary>
/// Contains type information for all properties involved in an updateable mapping.
/// </summary>
internal class UpdateableTypeContext
{
    private readonly Dictionary<string, PropertyTypeInfo> _propertyTypes = new();

    public void AddPropertyType(string propertyPath, ITypeSymbol type)
    {
        _propertyTypes[propertyPath] = new PropertyTypeInfo(propertyPath, type);
    }

    public PropertyTypeInfo GetPropertyType(string propertyPath)
    {
        _propertyTypes.TryGetValue(propertyPath, out var typeInfo);
        return typeInfo;
    }

    public bool CanPropertyBeNull(string propertyPath)
    {
        var typeInfo = GetPropertyType(propertyPath);
        return typeInfo?.CanBeNull ?? true; // Default to true for safety if type is unknown
    }

    public bool IsValueType(string propertyPath)
    {
        var typeInfo = GetPropertyType(propertyPath);
        return typeInfo?.IsValueType ?? false;
    }

    public bool IsNullableValueType(string propertyPath)
    {
        var typeInfo = GetPropertyType(propertyPath);
        return typeInfo?.IsNullableValueType ?? false;
    }
}