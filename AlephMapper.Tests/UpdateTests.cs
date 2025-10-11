namespace AlephMapper.Tests;

public class UpdateTests
{
    [Test]
    public async Task Should_Generate_Correct_UpdateMethod()
    {
        // Arrange
        var dest = new DestDto();
        var source = new SourceDto
        {
            Name = "John Doe",
            BirthInfo = new BirthInfo { Age = 30, Address = "123 Main St" },
            Email = "john@example.com"
        };

        // Act
        var result = Mapper.MapToDestDto(source, dest);

        // Assert
        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("John Doe");
        await Assert.That(dest.BirthInfo).IsNotNull();
        await Assert.That(dest.BirthInfo.Age).IsEqualTo(30);
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("123 Main St");
        await Assert.That(dest.ContactInfo).IsEqualTo("john@example.com");
    }

    [Test]
    public async Task Should_Generate_Correct_Nested_UpdateMethod()
    {
        // Arrange
        var dest = new TestModel1Dto();
        var source = new TestModel1
        {
            Name = "John Doe",
            SurName = "Smith", 
            Address = new Address1
            {
                Line1 = new AddressLine
                {
                    HouseNumber = "123",
                    Street = "Main St"
                },
                Line2 = new AddressLine
                {
                    HouseNumber = "456",
                    Street = "Second St"
                }
            }
        };

        // Act
        var result = TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert
        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("John Doe");
        await Assert.That(dest.SurName).IsEqualTo("Smith");
        await Assert.That(dest.Address).IsNotNull();
        await Assert.That(dest.Address.Line1).IsNotNull();
        await Assert.That(dest.Address.Line1.Street).IsEqualTo("Main St");
        await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("123");
        await Assert.That(dest.Address.Line2).IsNotNull();
        await Assert.That(dest.Address.Line2.Street).IsEqualTo("Second St");
        await Assert.That(dest.Address.Line2.HouseNumber).IsEqualTo("456");
    }

    [Test]
    public async Task Should_Handle_Null_Nested_Objects_Correctly()
    {
        // Arrange
        var dest = new TestModel1Dto();
        var source = new TestModel1
        {
            Name = "Jane Doe",
            SurName = "Johnson",
            Address = new Address1
            {
                Line1 = null, // Null nested object
                Line2 = new AddressLine
                {
                    HouseNumber = "789",
                    Street = "Third St"
                }
            }
        };

        // Act
        var result = TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert
        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("Jane Doe");
        await Assert.That(dest.SurName).IsEqualTo("Johnson");
        await Assert.That(dest.Address).IsNotNull();
        await Assert.That(dest.Address.Line1).IsNull(); // Should be null
        await Assert.That(dest.Address.Line2).IsNotNull();
        await Assert.That(dest.Address.Line2.Street).IsEqualTo("Third St");
        await Assert.That(dest.Address.Line2.HouseNumber).IsEqualTo("789");
    }

    [Test]
    public async Task Should_Update_Existing_Nested_Objects()
    {
        // Arrange - destination already has nested objects
        var dest = new TestModel1Dto
        {
            Name = "Old Name",
            SurName = "Old SurName", 
            Address = new Address1Dto
            {
                Line1 = new AddressLineDto { Street = "Old Street", HouseNumber = "999" },
                Line2 = new AddressLineDto { Street = "Old Street 2", HouseNumber = "888" }
            }
        };

        var source = new TestModel1
        {
            Name = "New Name",
            SurName = "New SurName",
            Address = new Address1
            {
                Line1 = new AddressLine
                {
                    HouseNumber = "123",
                    Street = "New Main St"
                },
                Line2 = new AddressLine
                {
                    HouseNumber = "456", 
                    Street = "New Second St"
                }
            }
        };

        // Keep references to existing objects to verify they're updated, not replaced
        var existingAddress = dest.Address;
        var existingLine1 = dest.Address.Line1;
        var existingLine2 = dest.Address.Line2;

        // Act
        var result = TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert - verify objects are updated, not replaced
        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Address).IsSameReferenceAs(existingAddress);
        await Assert.That(dest.Address.Line1).IsSameReferenceAs(existingLine1);
        await Assert.That(dest.Address.Line2).IsSameReferenceAs(existingLine2);
        
        // Verify values are updated
        await Assert.That(dest.Name).IsEqualTo("New Name");
        await Assert.That(dest.SurName).IsEqualTo("New SurName");
        await Assert.That(dest.Address.Line1.Street).IsEqualTo("New Main St");
        await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("123");
        await Assert.That(dest.Address.Line2.Street).IsEqualTo("New Second St");
        await Assert.That(dest.Address.Line2.HouseNumber).IsEqualTo("456");
    }
}