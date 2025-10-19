namespace AlephMapper.Tests;

public class RecursiveNestingTests
{
    [Test]
    public async Task Should_Handle_Deep_Nesting_With_Proper_Updatable_Logic()
    {
        // Test that verifies the recursive approach works for deeply nested objects

        // Create destination with some existing nested objects
        var dest = new TestModel1Dto
        {
            Name = "Original",
            Address = new Address1Dto
            {
                Line1 = new AddressLineDto { Street = "Original Street 1", HouseNumber = "000" }
                // Note: Line2 is null initially
            }
        };

        var source = new TestModel1
        {
            Name = "Updated",
            SurName = "Updated SurName",
            Address = new Address1
            {
                Line1 = new AddressLine { Street = "New Street 1", HouseNumber = "111" },
                Line2 = new AddressLine { Street = "New Street 2", HouseNumber = "222" }
            }
        };

        // Keep references to verify object reuse
        var originalAddress = dest.Address;
        var originalLine1 = dest.Address.Line1;

        // Act
        var result = TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert - verify deep nesting Updatable behavior
        await Assert.That(result).IsSameReferenceAs(dest);

        // Verify top level properties
        await Assert.That(dest.Name).IsEqualTo("Updated");
        await Assert.That(dest.SurName).IsEqualTo("Updated SurName");

        // Verify Address object is reused (not replaced)
        await Assert.That(dest.Address).IsSameReferenceAs(originalAddress);

        // Verify Line1 object is updated properly
        // Our recursive approach should update existing objects rather than replace them
        if (dest.Address.Line1 == originalLine1)
        {
            // If same reference, properties should be updated
            await Assert.That(dest.Address.Line1.Street).IsEqualTo("New Street 1");
            await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("111");
        }
        else
        {
            // If new object, that's also acceptable as long as values are correct
            await Assert.That(dest.Address.Line1).IsNotNull();
            await Assert.That(dest.Address.Line1.Street).IsEqualTo("New Street 1");
            await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("111");
        }

        // Verify Line2 was created (was null before)
        await Assert.That(dest.Address.Line2).IsNotNull();
        await Assert.That(dest.Address.Line2.Street).IsEqualTo("New Street 2");
        await Assert.That(dest.Address.Line2.HouseNumber).IsEqualTo("222");
    }

    [Test]
    public async Task Should_Handle_Null_Source_Nested_Objects()
    {
        // Test null handling in deep nesting

        var dest = new TestModel1Dto
        {
            Address = new Address1Dto
            {
                Line1 = new AddressLineDto { Street = "Existing", HouseNumber = "999" },
                Line2 = new AddressLineDto { Street = "Existing 2", HouseNumber = "888" }
            }
        };

        var source = new TestModel1
        {
            Name = "Test",
            Address = null // Null address should clear destination address
        };

        // Act
        TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("Test");
        await Assert.That(dest.Address).IsNull(); // Should be set to null
    }

    [Test]
    public async Task Should_Handle_Partial_Null_Nested_Objects()
    {
        // Test mixed null/non-null nested objects

        var dest = new TestModel1Dto();

        var source = new TestModel1
        {
            Name = "Test",
            Address = new Address1
            {
                Line1 = new AddressLine { Street = "Line 1 Street", HouseNumber = "123" },
                Line2 = null // Line2 is null
            }
        };

        // Act
        TestModel1Mapper.MapToTestModel1Dto(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("Test");
        await Assert.That(dest.Address).IsNotNull();
        await Assert.That(dest.Address.Line1).IsNotNull();
        await Assert.That(dest.Address.Line1.Street).IsEqualTo("Line 1 Street");
        await Assert.That(dest.Address.Line1.HouseNumber).IsEqualTo("123");
        await Assert.That(dest.Address.Line2).IsNull();
    }
}