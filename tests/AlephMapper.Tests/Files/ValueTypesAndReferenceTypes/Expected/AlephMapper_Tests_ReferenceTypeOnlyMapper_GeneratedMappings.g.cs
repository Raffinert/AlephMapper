using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class ReferenceTypeOnlyMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestination(ReferenceTypeOnlySource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ReferenceTypeOnlySource, ReferenceTypeOnlyDestination>> MapToDestinationExpression() => 
        source => new ReferenceTypeOnlyDestination
        {
            StringProperty = source.StringProperty,
            ObjectProperty = source.ObjectProperty != null
                ? new SimpleReferenceObject
                {
                    Name = source.ObjectProperty.Name
                }
                : null
        };

    /// <summary>
    /// Updates an existing instance of <see cref="ReferenceTypeOnlyDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static ReferenceTypeOnlyDestination MapToDestination(ReferenceTypeOnlySource source, ReferenceTypeOnlyDestination dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new ReferenceTypeOnlyDestination();
        dest.StringProperty = source.StringProperty;
        if (source.ObjectProperty != null)
        {
            if (dest.ObjectProperty == null)
                dest.ObjectProperty = new SimpleReferenceObject();
            dest.ObjectProperty.Name = source.ObjectProperty.Name;
        }
        else
        {
            dest.ObjectProperty = null;
        }
        return dest;
    }
}
