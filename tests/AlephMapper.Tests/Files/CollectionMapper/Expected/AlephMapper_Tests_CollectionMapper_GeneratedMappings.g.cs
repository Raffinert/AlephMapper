using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class CollectionMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestWithCollections(SourceWithCollections)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceWithCollections, DestWithCollections>> MapToDestWithCollectionsExpression() => 
        source => new DestWithCollections
        {
            Name = source.Name,
            Tags = source.Tags,
            Categories = source.Categories,
            NestedObject = source.NestedObject != null
                ? new NestedModel
                {
                    Value = source.NestedObject.Value,
                    NestedList = source.NestedObject.NestedList
                }
                : null
        };

    /// <summary>
    /// Updates an existing instance of <see cref="DestWithCollections"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestWithCollections MapToDestWithCollections(SourceWithCollections source, DestWithCollections dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestWithCollections();
        dest.Name = source.Name;
        // Skipping collection property: dest.Tags
        // Skipping collection property: dest.Categories
        if (source.NestedObject != null)
        {
            if (dest.NestedObject == null)
                dest.NestedObject = new NestedModel();
            dest.NestedObject.Value = source.NestedObject.Value;
            // Skipping collection property: dest.NestedObject.NestedList
        }
        else
        {
            dest.NestedObject = null;
        }
        return dest;
    }

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestSimple(SourceWithCollections)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceWithCollections, DestWithCollections>> MapToDestSimpleExpression() => 
        source => new DestWithCollections
        {
            Name = source.Name,
            NestedObject = source.NestedObject != null
                ? new NestedModel
                {
                    Value = source.NestedObject.Value
                }
                : null
        };

    /// <summary>
    /// Updates an existing instance of <see cref="DestWithCollections"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestWithCollections MapToDestSimple(SourceWithCollections source, DestWithCollections dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestWithCollections();
        dest.Name = source.Name;
        if (source.NestedObject != null)
        {
            if (dest.NestedObject == null)
                dest.NestedObject = new NestedModel();
            dest.NestedObject.Value = source.NestedObject.Value;
        }
        else
        {
            dest.NestedObject = null;
        }
        return dest;
    }
}
