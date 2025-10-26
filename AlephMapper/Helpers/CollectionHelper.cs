using Microsoft.CodeAnalysis;
using System.Linq;

namespace AlephMapper.Helpers;

/// <summary>
/// Utility class for collection type detection and analysis.
/// Provides centralized logic for determining if a type is a collection type
/// and what kind of collection it is.
/// </summary>
internal static class CollectionHelper
{
    /// <summary>
    /// Determines if a type is a collection type that should be handled specially.
    /// Collections are complex to update safely and may need special treatment in expressions.
    /// Uses proper Roslyn type symbol analysis instead of string matching.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is a collection type, false otherwise</returns>
    public static bool IsCollectionType(ITypeSymbol type)
    {
        if (type == null) return false;

        // Check for array types
        if (type.TypeKind == TypeKind.Array) return true;

        // Check if it's a string (special case - not treated as collection for most purposes)
        if (type.SpecialType == SpecialType.System_String) return false;

        // Check for specific collection types and interfaces
        if (type is INamedTypeSymbol namedType)
        {
            // Check if it's a generic collection type
            if (namedType.IsGenericType)
            {
                var originalDefinition = namedType.OriginalDefinition;
                var namespaceName = originalDefinition.ContainingNamespace?.ToDisplayString();
                var typeName = originalDefinition.Name;

                // Check common generic collection types
                if (namespaceName == "System.Collections.Generic")
                {
                    return IsGenericCollectionType(typeName);
                }

                // Check other collection namespaces
                if (namespaceName?.StartsWith("System.Collections.") == true)
                {
                    return IsCollectionNamespace(namespaceName);
                }
            }

            // Check if it implements IEnumerable<T> (but exclude string)
            foreach (var interfaceType in namedType.AllInterfaces)
            {
                if (interfaceType.IsGenericType)
                {
                    var interfaceDefinition = interfaceType.OriginalDefinition;
                    if (interfaceDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
                        interfaceDefinition.Name == "IEnumerable")
                    {
                        return true;
                    }
                }
                else
                {
                    // Check non-generic collection interfaces
                    var interfaceName = interfaceType.ToDisplayString();
                    if (IsNonGenericCollectionInterface(interfaceName))
                    {
                        return true;
                    }
                }
            }

            // Check if the type itself is a collection interface
            if (namedType.TypeKind == TypeKind.Interface)
            {
                return IsCollectionInterface(namedType);
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a List&lt;T&gt; type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is List&lt;T&gt;, false otherwise</returns>
    public static bool IsListType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
            return false;

        var originalDefinition = namedType.OriginalDefinition;
        return originalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
               originalDefinition.Name == "List";
    }

    /// <summary>
    /// Determines if a type is an array type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is an array, false otherwise</returns>
    public static bool IsArrayType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Array;
    }

    /// <summary>
    /// Gets the element type of a collection type.
    /// </summary>
    /// <param name="type">The collection type</param>
    /// <returns>The element type if it can be determined, null otherwise</returns>
    public static ITypeSymbol GetElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // For most generic collections, the first type argument is the element type
            return namedType.TypeArguments.FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Determines if a type implements IEnumerable&lt;T&gt; for any T.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type implements IEnumerable&lt;T&gt;, false otherwise</returns>
    public static bool ImplementsGenericIEnumerable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check if the type itself is IEnumerable<T>
        if (namedType.IsGenericType)
        {
            var originalDefinition = namedType.OriginalDefinition;
            if (originalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
                originalDefinition.Name == "IEnumerable")
            {
                return true;
            }
        }

        // Check implemented interfaces
        return namedType.AllInterfaces.Any(i =>
            i.IsGenericType &&
            i.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
            i.OriginalDefinition.Name == "IEnumerable");
    }

    private static bool IsGenericCollectionType(string typeName)
    {
        return typeName is "List" or "Dictionary" ||
               typeName == "HashSet" ||
               typeName == "Queue" ||
               typeName == "Stack" ||
               typeName == "SortedList" ||
               typeName == "SortedDictionary" ||
               typeName == "SortedSet" ||
               typeName == "LinkedList" ||
               typeName == "ICollection" ||
               typeName == "IList" ||
               typeName == "IDictionary" ||
               typeName == "ISet" ||
               typeName == "IEnumerable" ||
               typeName == "IReadOnlyCollection" ||
               typeName == "IReadOnlyList" ||
               typeName == "IReadOnlyDictionary" ||
               typeName == "IReadOnlySet";
    }

    private static bool IsCollectionNamespace(string namespaceName)
    {
        return namespaceName == "System.Collections.Concurrent" ||
               namespaceName == "System.Collections.ObjectModel" ||
               namespaceName == "System.Collections.Specialized" ||
               namespaceName == "System.Collections.Immutable";
    }

    private static bool IsNonGenericCollectionInterface(string interfaceName)
    {
        return interfaceName == "System.Collections.IEnumerable" ||
               interfaceName == "System.Collections.ICollection" ||
               interfaceName == "System.Collections.IList" ||
               interfaceName == "System.Collections.IDictionary";
    }

    private static bool IsCollectionInterface(INamedTypeSymbol namedType)
    {
        var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
        var typeName = namedType.Name;

        if (namespaceName == "System.Collections.Generic")
        {
            return typeName == "ICollection" ||
                   typeName == "IList" ||
                   typeName == "IDictionary" ||
                   typeName == "ISet" ||
                   typeName == "IEnumerable" ||
                   typeName == "IReadOnlyCollection" ||
                   typeName == "IReadOnlyList" ||
                   typeName == "IReadOnlyDictionary" ||
                   typeName == "IReadOnlySet";
        }

        if (namespaceName == "System.Collections")
        {
            return typeName == "IEnumerable" ||
                   typeName == "ICollection" ||
                   typeName == "IList" ||
                   typeName == "IDictionary";
        }

        return false;
    }
}