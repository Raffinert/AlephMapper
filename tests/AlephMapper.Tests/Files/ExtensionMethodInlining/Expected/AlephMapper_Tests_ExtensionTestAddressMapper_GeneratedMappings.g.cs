using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class ExtensionTestAddressMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(ExtensionTestAddress)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ExtensionTestAddress, ExtensionTestAddressDto>> ToDtoExpression() => 
        address => new ExtensionTestAddressDto
        {
            Street = address.Street,
            City = address.City,
            PostalCode = address.PostalCode,
            FormattedAddress = $"{address.Street}, {address.City} {address.PostalCode}"
        };
}
