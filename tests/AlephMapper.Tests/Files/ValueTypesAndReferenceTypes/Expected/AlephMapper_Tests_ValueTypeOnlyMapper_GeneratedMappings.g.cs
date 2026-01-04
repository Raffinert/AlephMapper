using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class ValueTypeOnlyMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestination(ValueTypeOnlySource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ValueTypeOnlySource, ValueTypeOnlyDestination>> MapToDestinationExpression() => 
        source => new ValueTypeOnlyDestination
        {
            IntProperty = source.IntProperty,
            BoolProperty = source.BoolProperty,
            DateTimeProperty = source.DateTimeProperty,
            DecimalProperty = source.DecimalProperty
        };

    /// <summary>
    /// Updates an existing instance of <see cref="ValueTypeOnlyDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static ValueTypeOnlyDestination MapToDestination(ValueTypeOnlySource source, ValueTypeOnlyDestination dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new ValueTypeOnlyDestination();
        dest.IntProperty = source.IntProperty;
        dest.BoolProperty = source.BoolProperty;
        dest.DateTimeProperty = source.DateTimeProperty;
        dest.DecimalProperty = source.DecimalProperty;
        return dest;
    }
}
