using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;
using TUnit.Core;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class MixedTypeMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestination(MixedTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<MixedTypeSource, MixedTypeDestination>> MapToDestinationExpression() => 
        source => new MixedTypeDestination
        {
            IntValue = source.IntValue,
            StringValue = source.StringValue,
            BoolValue = source.BoolValue,
            DecimalValue = source.DecimalValue,
            DateTimeValue = source.DateTimeValue,
            NullableIntValue = source.NullableIntValue,
            ReferenceObject = source.ReferenceObject != null
                ? new TestObject
                {
                    Name = source.ReferenceObject.Name,
                    Value = source.ReferenceObject.Value
                }
                : null
        };

    /// <summary>
    /// Updates an existing instance of <see cref="MixedTypeDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static MixedTypeDestination MapToDestination(MixedTypeSource source, MixedTypeDestination dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new MixedTypeDestination();
        dest.IntValue = source.IntValue;
        dest.StringValue = source.StringValue;
        dest.BoolValue = source.BoolValue;
        dest.DecimalValue = source.DecimalValue;
        dest.DateTimeValue = source.DateTimeValue;
        dest.NullableIntValue = source.NullableIntValue;
        if (source.ReferenceObject != null)
        {
            if (dest.ReferenceObject == null)
                dest.ReferenceObject = new TestObject();
            dest.ReferenceObject.Name = source.ReferenceObject.Name;
            dest.ReferenceObject.Value = source.ReferenceObject.Value;
        }
        else
        {
            dest.ReferenceObject = null;
        }
        return dest;
    }
}
