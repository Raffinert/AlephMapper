namespace AlephMapper.Tests;

public class SourceIndependentTargetCreationTests
{
    [Test]
    public async Task Should_Create_Target_Objects_Independent_Of_Source_Conditions()
    {
        // This test verifies that target object creation is independent of source null checks
        // The key insight: when we need to update a property based on source data,
        // we should ALWAYS ensure the target object exists, regardless of source conditions
        
        var dest = new DestDto
        {
            Name = "Original",
            BirthInfo = null, // Start with null target
            ContactInfo = "original@example.com"
        };

        var source = new SourceDto
        {
            Name = "Updated",
            BirthInfo = new BirthInfo { Age = 30, Address = "Kyiv" }, // Source is NOT null
            Email = "updated@example.com"
        };

        // Act
        Mapper.MapToDestDto(source, dest);

        // Assert: Target should be created because source is not null
        await Assert.That(dest.BirthInfo).IsNotNull();
        await Assert.That(dest.BirthInfo.Age).IsEqualTo(30);
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("Kyiv");
    }

    [Test]
    public async Task Should_Reuse_Existing_Target_Objects_Independent_Of_Source_Conditions()
    {
        // This test verifies that when target object exists, it's reused regardless of source conditions
        
        var existingBirthInfo = new BirthInfoDto { Age = 999, Address = "Original" };
        var dest = new DestDto
        {
            Name = "Original",
            BirthInfo = existingBirthInfo, // Target exists
            ContactInfo = "original@example.com"
        };

        var source = new SourceDto
        {
            Name = "Updated",
            BirthInfo = new BirthInfo { Age = 30, Address = "Kyiv" }, // Source is NOT null
            Email = "updated@example.com"
        };

        // Act
        Mapper.MapToDestDto(source, dest);

        // Assert: Existing target should be reused (same reference) but properties updated
        await Assert.That(dest.BirthInfo).IsSameReferenceAs(existingBirthInfo);
        await Assert.That(dest.BirthInfo.Age).IsEqualTo(30); // Properties updated
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("Kyiv");
    }

    [Test]
    public async Task Should_Set_Target_To_Null_When_Source_Is_Null()
    {
        // This test verifies that when source is null, target is set to null
        // But this is about the SOURCE condition, not target object creation
        
        var existingBirthInfo = new BirthInfoDto { Age = 999, Address = "Original" };
        var dest = new DestDto
        {
            Name = "Original",
            BirthInfo = existingBirthInfo, // Target exists
            ContactInfo = "original@example.com"
        };

        var source = new SourceDto
        {
            Name = "Updated",
            BirthInfo = null, // Source is null
            Email = "updated@example.com"
        };

        // Act
        Mapper.MapToDestDto(source, dest);

        // Assert: Target should be set to null because SOURCE is null
        await Assert.That(dest.BirthInfo).IsNull();
        await Assert.That(dest.Name).IsEqualTo("Updated");
        await Assert.That(dest.ContactInfo).IsEqualTo("updated@example.com");
    }

    [Test]
    public async Task Should_Handle_Deep_Nesting_With_Independent_Target_Creation()
    {
        // Test the corrected behavior with deep nesting
        
        var dest = new TestModel1Dto(); // All nulls initially

        var source = new TestModel1
        {
            Name = "Test",
            SurName = "User",
            Address = new Address1
            {
                Line1 = new AddressLine { Street = "Main St", HouseNumber = "123" },
                Line2 = null // Mixed: Line1 exists, Line2 is null
            }
        };

        // Act
        TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert: Target objects should be created independently based on what needs updating
        await Assert.That(dest.Address).IsNotNull(); // Address created because source.Address != null
        await Assert.That(dest.Address.Line1).IsNotNull(); // Line1 created because source.Address.Line1 != null
        await Assert.That(dest.Address.Line1.Street).IsEqualTo("Main St");
        await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("123");
        await Assert.That(dest.Address.Line2).IsNull(); // Line2 is null because source.Address.Line2 == null
    }

    [Test] 
    public async Task Should_Update_Existing_Deep_Nested_Objects()
    {
        // Test that existing deep nested objects are reused, not replaced
        
        var existingLine1 = new AddressLineDto { Street = "Old Street", HouseNumber = "999" };
        var existingLine2 = new AddressLineDto { Street = "Old Street 2", HouseNumber = "888" };
        var existingAddress = new Address1Dto { Line1 = existingLine1, Line2 = existingLine2 };
        
        var dest = new TestModel1Dto
        {
            Name = "Old",
            Address = existingAddress
        };

        var source = new TestModel1
        {
            Name = "New",
            SurName = "User", 
            Address = new Address1
            {
                Line1 = new AddressLine { Street = "New Street", HouseNumber = "123" },
                Line2 = new AddressLine { Street = "New Street 2", HouseNumber = "456" }
            }
        };

        // Act
        TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert: Existing objects should be reused and updated
        await Assert.That(dest.Address).IsSameReferenceAs(existingAddress);
        await Assert.That(dest.Address.Line1).IsSameReferenceAs(existingLine1);
        await Assert.That(dest.Address.Line2).IsSameReferenceAs(existingLine2);
        
        // But properties should be updated
        await Assert.That(dest.Address.Line1.Street).IsEqualTo("New Street");
        await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("123");
        await Assert.That(dest.Address.Line2.Street).IsEqualTo("New Street 2");
        await Assert.That(dest.Address.Line2.HouseNumber).IsEqualTo("456");
    }
}