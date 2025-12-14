using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class CircularMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ProcessValue(CircularTestModel)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<CircularTestModel, string>> ProcessValueExpression() => 
        source => source.Value.ToUpper() ?? "";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="UpdateSimpleDto(CircularTestModel)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<CircularTestModel, CircularDto>> UpdateSimpleDtoExpression() => 
        source => new CircularDto
        {
            ProcessedValue = source.Value.ToUpper() ?? "" // Direct assignment without method call
        };

    /// <summary>
    /// Updates an existing instance of <see cref="CircularDto"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static CircularDto UpdateSimpleDto(CircularTestModel source, CircularDto dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new CircularDto();
        dest.ProcessedValue = source?.Value?.ToUpper() ?? "";
        return dest;
    }
}
