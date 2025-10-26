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
    //[Expressive]
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
    //[Expressive]
    [Updatable]
    public static ExtensionTestPersonDto ToDto(ExtensionTestPerson person) => new()
    {
        Id = person.Id,
        Name = person.Name,
        Address = person.Address?.ToDto() // This should be inlined (MemberBindingExpressionSyntax)
    };
}

public class ExtensionMethodInliningTests
{
    [Test]
    public async Task ExtensionMethodShouldBeInlined()
    {
        // Act
        var expression = ExtensionTestPersonMapper.ToDtoExpression();
        var readable = expression.ToReadableString();

        // Assert - The extension method call should be inlined
        var expected = """
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
                        }
                        """;

        await Assert.That(readable).Contains("new ExtensionTestAddressDto");
        await Assert.That(readable).Contains("person.Address.Street");
        await Assert.That(readable).Contains("person.Address.City");
        await Assert.That(readable).Contains("person.Address.PostalCode");
        
        // Should not contain the extension method call
        await Assert.That(readable).DoesNotContain(".ToDto()");
        
        Console.WriteLine("Generated Expression:");
        Console.WriteLine(readable);
    }

    [Test]
    public async Task ConditionalAccessExtensionMethodShouldBeInlined()
    {
        // Act
        var expression = ConditionalExtensionTestPersonMapper.ToDtoExpression();
        var readable = expression.ToReadableString();

        // Assert - The conditional access extension method call should be inlined
        await Assert.That(readable).Contains("new ExtensionTestAddressDto");
        await Assert.That(readable).Contains("person.Address.Street");
        await Assert.That(readable).Contains("person.Address.City");
        await Assert.That(readable).Contains("person.Address.PostalCode");
        
        // Should not contain the extension method call
        await Assert.That(readable).DoesNotContain("?.ToDto()");
        
        // Should contain conditional access logic (null check) - this test may need adjustment
        // await Assert.That(readable).Contains("person.Address != null");
        
        Console.WriteLine("Generated Expression (Conditional Access):");
        Console.WriteLine(readable);
    }

    [Test] 
    public async Task ConditionalAccessExtensionMethodUpdatableShouldWork()
    {
        // Test that the updatable method generates correctly and works
        // Arrange
        var person = new ExtensionTestPerson
        {
            Id = 1,
            Name = "John Doe",
            Address = new ExtensionTestAddress
            {
                Street = "123 Main St",
                City = "New York", 
                PostalCode = "10001"
            }
        };

        var dto = new ExtensionTestPersonDto();

        // Act - Use the generated update method
        var result = ConditionalExtensionTestPersonMapper.ToDto(person, dto);

        // Assert
        await Assert.That(result).IsSameReferenceAs(dto);
        await Assert.That(dto.Id).IsEqualTo(1);
        await Assert.That(dto.Name).IsEqualTo("John Doe");
        await Assert.That(dto.Address).IsNotNull();
        await Assert.That(dto.Address.Street).IsEqualTo("123 Main St");
        await Assert.That(dto.Address.City).IsEqualTo("New York");
        await Assert.That(dto.Address.PostalCode).IsEqualTo("10001");
        await Assert.That(dto.Address.FormattedAddress).IsEqualTo("123 Main St, New York 10001");
        
        // Test with null address
        var personWithNullAddress = new ExtensionTestPerson
        {
            Id = 2,
            Name = "Jane Doe", 
            Address = null
        };

        var dto2 = new ExtensionTestPersonDto();
        var result2 = ConditionalExtensionTestPersonMapper.ToDto(personWithNullAddress, dto2);

        await Assert.That(result2).IsSameReferenceAs(dto2);
        await Assert.That(dto2.Id).IsEqualTo(2);
        await Assert.That(dto2.Name).IsEqualTo("Jane Doe");
        await Assert.That(dto2.Address).IsNull();
    }

    [Test]
    public async Task InMemoryMappingShouldWork()
    {
        // Arrange
        var person = new ExtensionTestPerson
        {
            Id = 1,
            Name = "John Doe",
            Address = new ExtensionTestAddress
            {
                Street = "123 Main St",
                City = "New York",
                PostalCode = "10001"
            }
        };

        // Act
        var dto = ExtensionTestPersonMapper.ToDto(person);

        // Assert
        await Assert.That(dto.Id).IsEqualTo(1);
        await Assert.That(dto.Name).IsEqualTo("John Doe");
        await Assert.That(dto.Address.Street).IsEqualTo("123 Main St");
        await Assert.That(dto.Address.City).IsEqualTo("New York");
        await Assert.That(dto.Address.PostalCode).IsEqualTo("10001");
        await Assert.That(dto.Address.FormattedAddress).IsEqualTo("123 Main St, New York 10001");
    }

    [Test]
    public async Task ConditionalAccessInMemoryMappingShouldWork()
    {
        // Arrange - Test with non-null address
        var personWithAddress = new ExtensionTestPerson
        {
            Id = 1,
            Name = "John Doe",
            Address = new ExtensionTestAddress
            {
                Street = "123 Main St",
                City = "New York",
                PostalCode = "10001"
            }
        };

        // Act
        var dtoWithAddress = ConditionalExtensionTestPersonMapper.ToDto(personWithAddress);

        // Assert
        await Assert.That(dtoWithAddress.Id).IsEqualTo(1);
        await Assert.That(dtoWithAddress.Name).IsEqualTo("John Doe");
        await Assert.That(dtoWithAddress.Address).IsNotNull();
        await Assert.That(dtoWithAddress.Address.Street).IsEqualTo("123 Main St");
        await Assert.That(dtoWithAddress.Address.City).IsEqualTo("New York");
        await Assert.That(dtoWithAddress.Address.PostalCode).IsEqualTo("10001");
        await Assert.That(dtoWithAddress.Address.FormattedAddress).IsEqualTo("123 Main St, New York 10001");

        // Arrange - Test with null address
        var personWithoutAddress = new ExtensionTestPerson
        {
            Id = 2,
            Name = "Jane Doe",
            Address = null
        };

        // Act
        var dtoWithoutAddress = ConditionalExtensionTestPersonMapper.ToDto(personWithoutAddress);

        // Assert
        await Assert.That(dtoWithoutAddress.Id).IsEqualTo(2);
        await Assert.That(dtoWithoutAddress.Name).IsEqualTo("Jane Doe");
        await Assert.That(dtoWithoutAddress.Address).IsNull();
    }
}