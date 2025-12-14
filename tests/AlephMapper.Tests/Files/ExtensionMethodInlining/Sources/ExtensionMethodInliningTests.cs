using AgileObjects.ReadableExpressions;

namespace AlephMapper.Tests;

// Test models for extension method testing
public class ExtensionTestAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public class ExtensionTestAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string FormattedAddress { get; set; } = string.Empty;
}

public class ExtensionTestPerson
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; 
    public ExtensionTestAddress? Address { get; set; }
}

public class ExtensionTestPersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ExtensionTestAddressDto? Address { get; set; }
}

// Extension method mapper
public static partial class ExtensionTestAddressMapper
{
    [Expressive]
    public static ExtensionTestAddressDto ToDto(this ExtensionTestAddress address) => new()
    {
        Street = address.Street,
        City = address.City,
        PostalCode = address.PostalCode,
        FormattedAddress = $"{address.Street}, {address.City} {address.PostalCode}"
    };
}

// Main mapper that uses the extension method
public static partial class ExtensionTestPersonMapper
{
    [Expressive]
    public static ExtensionTestPersonDto ToDto(ExtensionTestPerson person) => new()
    {
        Id = person.Id,
        Name = person.Name,
        Address = person.Address.ToDto() // This should be inlined
    };
}

// Main mapper that uses conditional access extension method
public static partial class ConditionalExtensionTestPersonMapper
{
    [Expressive]
    public static ExtensionTestPersonDto ToDto(ExtensionTestPerson person) => new()
    {
        Id = person.Id,
        Name = person.Name,
        Address = person.Address?.ToDto() // This should be inlined (MemberBindingExpressionSyntax)
    };
}