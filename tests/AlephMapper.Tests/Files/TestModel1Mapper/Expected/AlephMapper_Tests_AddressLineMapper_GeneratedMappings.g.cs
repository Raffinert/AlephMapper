using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class AddressLineMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDto(AddressLine)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<AddressLine, AddressLineDto>> MapToDtoExpression() => 
        sourceAddressLine1 => sourceAddressLine1 == null
            ? null 
            : new AddressLineDto
            {
                Street = sourceAddressLine1.Street,
                HouseNumber = sourceAddressLine1.HouseNumber
            };
}
