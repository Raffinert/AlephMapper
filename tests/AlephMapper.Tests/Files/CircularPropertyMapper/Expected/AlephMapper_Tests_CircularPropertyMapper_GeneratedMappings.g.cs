using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class CircularPropertyMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="UpdateTypeA(CircularPropsSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<CircularPropsSource, TypeA>> UpdateTypeAExpression() => 
        source => new TypeA
        {
            Name = source.Name,
            B = new TypeB
            {
                A = new TypeA
                {
                    Name = source.Name
                }
            }
        };

    /// <summary>
    /// Updates an existing instance of <see cref="TypeA"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static TypeA UpdateTypeA(CircularPropsSource source, TypeA dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new TypeA();
        dest.Name = source?.Name;
        if (dest.B == null)
            dest.B = new TypeB();
        if (dest.B.A == null)
            dest.B.A = new TypeA();
        dest.B.A.Name = source?.Name;
        return dest;
    }
}
