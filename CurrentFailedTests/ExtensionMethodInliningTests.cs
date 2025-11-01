using AgileObjects.ReadableExpressions;
using AlephMapper;

namespace CurrentFailedTests;

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
    public List<ExtensionTestAddress>? Addresses { get; set; }
    public ExtensionTestAddress? HomeAddress { get; set; }
}

public class ExtensionTestPersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ExtensionTestAddressDto>? Addresses { get; set; }

    public ExtensionTestAddressDto? HomeAddress { get; set; }
}

public static partial class ExtensionTestAddressMapper
{
    //[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static ExtensionTestAddressDto ToDto(this ExtensionTestAddress address) => new()
    {
        Street = address.Street,
        City = address.City,
        PostalCode = address.PostalCode,
        FormattedAddress = $"{address.Street}, {address.City} {address.PostalCode}"
    };
}

// Main mapper that uses conditional access extension method
public static partial class ConditionalExtensionTestPersonMapper
{
    [Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static ExtensionTestPersonDto ToDto(ExtensionTestPerson person) => new()
    {
        //Name = person?.Name,
        //Addresses = person?.Addresses.Select(ExtensionTestAddressMapper.ToDto).ToList(),
        HomeAddress = person?.HomeAddress?.ToDto()
    };

    [Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
    public static ExtensionTestPersonDto ToDto1(ExtensionTestPerson person) => new()
    {
        //Name = person.Name,
        //Addresses = person.Addresses?.Select(a => a.ToDto()).ToList(),
        HomeAddress = person?.HomeAddress?.ToDto()
    };
}

public class ExtensionMethodInliningTests
{
    [Test]
    public async Task ConditionalAccessExtensionMethodShouldBeInlined()
    {
        // Act
        var expression = ConditionalExtensionTestPersonMapper.ToDtoExpression();
        var readable = expression.ToReadableString();

        // Assert - The conditional access extension method call should be inlined
        await Assert.That(readable).Contains("new ExtensionTestAddressDto");
        await Assert.That(readable).Contains("address.Street");
        await Assert.That(readable).Contains("address.City");
        await Assert.That(readable).Contains("address.PostalCode");
        
        // Should not contain the extension method call
        await Assert.That(readable).DoesNotContain("?.ToDto()");
        
        // Should contain conditional access logic (null check) - this test may need adjustment
        // await Assert.That(readable).Contains("person.Address != null");
        
        Console.WriteLine("Generated Expression (Conditional Access):");
        Console.WriteLine(readable);
    }
}