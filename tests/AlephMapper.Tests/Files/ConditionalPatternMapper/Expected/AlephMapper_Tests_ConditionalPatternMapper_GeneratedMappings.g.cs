using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class ConditionalPatternMapper
{
    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel BothSidesObjects(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Value == null)
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = "Default";
            dest.Nested.Number = 0;
        }
        else
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Name;
            dest.Nested.Number = source.Value.Value;
        }
        return dest;
    }

    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel ObjectThenNull(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Name != null)
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Name;
            dest.Nested.Number = 42;
        }
        else
        {
            dest.Nested = null;
        }
        return dest;
    }

    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel NullThenObject(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Name == null)
        {
            dest.Nested = null;
        }
        else
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Name;
            dest.Nested.Number = 42;
        }
        return dest;
    }

    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel NestedBothSides(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Nested?.Content == null)
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = "Fallback";
            dest.Nested.Number = -1;
        }
        else
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Nested.Content;
            dest.Nested.Number = source.Nested.Number;
        }
        return dest;
    }

    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel ObjectThenThrow(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Name != null)
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Name;
            dest.Nested.Number = 42;
        }
        else
        {
            throw new ArgumentNullException(nameof(source.Name));
        }
        return dest;
    }

    /// <summary>
    /// Updates an existing instance of <see cref="DestModel"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestModel ThrowThenObject(SourceModel source, DestModel dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestModel();
        dest.Name = source.Name;
        if (source.Name == null)
        {
            throw new ArgumentNullException(nameof(source.Name));
        }
        else
        {
            if (dest.Nested == null)
                dest.Nested = new NestedDest();
            dest.Nested.Content = source.Name;
            dest.Nested.Number = 42;
        }
        return dest;
    }
}
