using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class ComplexPropertyMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Map(ComplexPropertyTestSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ComplexPropertyTestSource, ComplexPropertyTestDestination>> MapExpression() => 
        source => new ComplexPropertyTestDestination
        {
            NestedClass = new NestedReferenceType
            {
                InnerValue = source.NestedStruct.InnerValue,
                InnerClass = new DeeplyNestedReferenceType
                {
                    DeepValue = source.NestedStruct.InnerStruct.DeepValue
                }
            }
        };

    /// <summary>
    /// Updates an existing instance of <see cref="ComplexPropertyTestDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static ComplexPropertyTestDestination Map(ComplexPropertyTestSource source, ComplexPropertyTestDestination dest)
    {
        if (dest == null)
            dest = new ComplexPropertyTestDestination();
        if (dest.NestedClass == null)
            dest.NestedClass = new NestedReferenceType();
        dest.NestedClass.InnerValue = source.NestedStruct.InnerValue;
        if (dest.NestedClass.InnerClass == null)
            dest.NestedClass.InnerClass = new DeeplyNestedReferenceType();
        dest.NestedClass.InnerClass.DeepValue = source.NestedStruct.InnerStruct.DeepValue;
        return dest;
    }
}
