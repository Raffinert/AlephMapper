using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.3")]
partial class SimpleValueToReferenceMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Map(SimpleValueTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SimpleValueTypeSource, SimpleReferenceTypeDestination>> MapExpression() => 
        source => new SimpleReferenceTypeDestination
        {
            Value = source.Value,
            Name = source.Name
        };

    /// <summary>
    /// Updates an existing or create new instance of <see cref="SimpleReferenceTypeDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, the new instance is created.</param>
    /// <returns>The updated destination object for method chaining, or the new destination instance if either parameter is null.</returns>
    public static SimpleReferenceTypeDestination Map(SimpleValueTypeSource source, SimpleReferenceTypeDestination dest)
    {
        if (dest == null)
            dest = new SimpleReferenceTypeDestination();
        dest.Value = source.Value;
        dest.Name = source.Name;
        return dest;
    }
}
