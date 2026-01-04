using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class EdgeCaseMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Map(EdgeCaseValueTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<EdgeCaseValueTypeSource, EdgeCaseReferenceTypeDestination>> MapExpression() => 
        source => new EdgeCaseReferenceTypeDestination
        {
            SimpleValue = source.SimpleValue,
            ComplexValue = new EdgeCaseReferenceTypeClass
            {
                StructValue = source.ComplexValue.StructValue,
                NestedClass = new NestedEdgeCaseClass
                {
                    Value = source.ComplexValue.NestedStruct.Value
                }
            }
        };

    /// <summary>
    /// Updates an existing instance of <see cref="EdgeCaseReferenceTypeDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static EdgeCaseReferenceTypeDestination Map(EdgeCaseValueTypeSource source, EdgeCaseReferenceTypeDestination dest)
    {
        if (dest == null)
            dest = new EdgeCaseReferenceTypeDestination();
        dest.SimpleValue = source.SimpleValue;
        if (dest.ComplexValue == null)
            dest.ComplexValue = new EdgeCaseReferenceTypeClass();
        dest.ComplexValue.StructValue = source.ComplexValue.StructValue;
        if (dest.ComplexValue.NestedClass == null)
            dest.ComplexValue.NestedClass = new NestedEdgeCaseClass();
        dest.ComplexValue.NestedClass.Value = source.ComplexValue.NestedStruct.Value;
        return dest;
    }
}
