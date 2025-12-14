using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class ExtensionTestPersonMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(ExtensionTestPerson)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ExtensionTestPerson, ExtensionTestPersonDto>> ToDtoExpression() => 
        person => new ExtensionTestPersonDto
        {
            Id = person.Id,
            Name = person.Name,
            Address = new ExtensionTestAddressDto
            {
                Street = person.Address.Street,
                City = person.Address.City,
                PostalCode = person.Address.PostalCode,
                FormattedAddress = $"{person.Address.Street}, {person.Address.City} {person.Address.PostalCode}"
            }
        };
}
