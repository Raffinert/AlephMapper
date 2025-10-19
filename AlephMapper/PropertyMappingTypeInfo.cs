using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace AlephMapper;

/// <summary>
/// Represents type information for a property path during Updatable method generation.
/// This helps determine whether null checks are needed based on the actual type.
/// </summary>
internal class PropertyMappingTypeInfo
{
    public PropertyMappingTypeInfo(string propertyPath, ITypeSymbol type)
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
        IsCollectionType = CollectionHelper.IsCollectionType(type);
        TypeDisplayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public string PropertyPath { get; }
    public ITypeSymbol Type { get; }
    public bool IsValueType { get; }
    public bool IsNullableValueType { get; }
    public bool IsReferenceType { get; }
    public bool CanBeNull { get; }
    public bool IsCollectionType { get; }
    public string TypeDisplayName { get; }
}

/// <summary>
/// Contains type information for all properties involved in an Updatable mapping.
/// Provides a context for analyzing property types during mapping code generation.
/// </summary>
internal class PropertyMappingContext
{
    private readonly Dictionary<string, PropertyMappingTypeInfo> _propertyTypes = new();

    public void AddPropertyType(string propertyPath, ITypeSymbol type)
    {
        _propertyTypes[propertyPath] = new PropertyMappingTypeInfo(propertyPath, type);
    }

    public PropertyMappingTypeInfo GetPropertyType(string propertyPath)
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

    public bool IsCollectionType(string propertyPath)
    {
        var typeInfo = GetPropertyType(propertyPath);

        // If we have type information, use it
        if (typeInfo != null)
        {
            return typeInfo.IsCollectionType;
        }

        return false;

        // Fallback: Check property name patterns that suggest collections
        // This helps when type information is not available
        //var propertyName = propertyPath.Split('.').Last().ToLowerInvariant();
        //return propertyName.EndsWith("list") ||
        //       propertyName.EndsWith("collection") ||
        //       propertyName.EndsWith("set") ||
        //       propertyName.EndsWith("array") ||
        //       propertyName.EndsWith("items") ||
        //       propertyName.EndsWith("tags") ||
        //       propertyName.EndsWith("categories") ||
        //       propertyName.EndsWith("numbers") ||
        //       propertyName == "metadata";
    }
}