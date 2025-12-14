using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class Address1Mapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDto(Address1)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Address1, Address1Dto>> MapToDtoExpression() => 
        sourceAddress => new Address1Dto
        {
            Line1 = sourceAddress.Line1 == null
                ? null 
                : new AddressLineDto
                {
                    Street = sourceAddress.Line1.Street,
                    HouseNumber = sourceAddress.Line1.HouseNumber
                },
            Line2 = sourceAddress.Line2 == null
                ? null 
                : new AddressLineDto
                {
                    Street = sourceAddress.Line2.Street,
                    HouseNumber = sourceAddress.Line2.HouseNumber
                }
        };
}
