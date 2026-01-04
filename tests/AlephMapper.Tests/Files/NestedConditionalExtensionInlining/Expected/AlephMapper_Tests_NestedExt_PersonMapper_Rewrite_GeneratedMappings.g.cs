using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class NestedExt_PersonMapper_Rewrite
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(NestedExt_Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<NestedExt_Person, NestedExt_PersonDto>> ToDtoExpression() => 
        p => new NestedExt_PersonDto
        {
            HomeAddress = (p != null
                ? ((p.Friend != null
                    ? ((p.Friend.HomeAddress != null
                        ? (new NestedExt_AddressDto
                        {
                            Street = p.Friend.HomeAddress.Street,
                            City = p.Friend.HomeAddress.City
                        }) 
                        : (NestedExt_AddressDto)null)) 
                    : (NestedExt_AddressDto)null)) 
                : (NestedExt_AddressDto)null)
        };
}
