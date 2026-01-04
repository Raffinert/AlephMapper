using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class NullableValueTypeMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestination(NullableValueTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<NullableValueTypeSource, NullableValueTypeDestination>> MapToDestinationExpression() => 
        source => new NullableValueTypeDestination
        {
            NullableIntProperty = source.NullableIntProperty,
            NullableBoolProperty = source.NullableBoolProperty,
            NullableDateTimeProperty = source.NullableDateTimeProperty
        };

    /// <summary>
    /// Updates an existing instance of <see cref="NullableValueTypeDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static NullableValueTypeDestination MapToDestination(NullableValueTypeSource source, NullableValueTypeDestination dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new NullableValueTypeDestination();
        dest.NullableIntProperty = source.NullableIntProperty;
        dest.NullableBoolProperty = source.NullableBoolProperty;
        dest.NullableDateTimeProperty = source.NullableDateTimeProperty;
        return dest;
    }
}
