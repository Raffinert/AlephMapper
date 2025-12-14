using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class NestedExt_AddressMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(NestedExt_Address)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<NestedExt_Address, NestedExt_AddressDto>> ToDtoExpression() => 
        a => new NestedExt_AddressDto
        {
            Street = a.Street,
            City = a.City
        };
}
