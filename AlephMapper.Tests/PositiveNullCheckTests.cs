namespace AlephMapper.Tests;

public class PositiveNullCheckTests
{
    [Test]
    public async Task Should_Handle_Positive_Null_Check_Correctly()
    {
        // Test MapToDestDto which uses: source.BirthInfo != null ? MapToBirthInfoDto(source.BirthInfo) : null
        
        // Test case 1: Source has BirthInfo (positive condition is true)
        var dest1 = new DestDto();
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "John Doe",
            BirthInfo = new BirthInfo { Age = 30, Address = "Kyiv" },
            Email = "john@example.com"
        };

        var result1 = Mapper.MapToDestDto(sourceWithBirthInfo, dest1);

        await Assert.That(result1).IsSameReferenceAs(dest1);
        await Assert.That(dest1.Name).IsEqualTo("John Doe");
        await Assert.That(dest1.BirthInfo).IsNotNull();
        await Assert.That(dest1.BirthInfo.Age).IsEqualTo(30);
        await Assert.That(dest1.BirthInfo.Address).IsEqualTo("Kyiv");
        await Assert.That(dest1.ContactInfo).IsEqualTo("john@example.com");

        // Test case 2: Source has null BirthInfo (positive condition is false)
        var dest2 = new DestDto();
        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "Jane Doe",
            BirthInfo = null,
            Email = "jane@example.com"
        };

        var result2 = Mapper.MapToDestDto(sourceWithNullBirthInfo, dest2);

        await Assert.That(result2).IsSameReferenceAs(dest2);
        await Assert.That(dest2.Name).IsEqualTo("Jane Doe");
        await Assert.That(dest2.BirthInfo).IsNull();
        await Assert.That(dest2.ContactInfo).IsEqualTo("jane@example.com");
    }

    [Test]
    public async Task Should_Handle_Negative_Null_Check_Correctly()
    {
        // Test MapToDestDto1 which uses: source.BirthInfo == null ? null : MapToBirthInfoDto(source.BirthInfo)
        
        // Test case 1: Source has BirthInfo (negative condition is false)
        var dest1 = new DestDto();
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "John Doe",
            BirthInfo = new BirthInfo { Age = 35, Address = "Lviv" },
            Email = "john@example.com"
        };

        var result1 = Mapper.MapToDestDto1(sourceWithBirthInfo, dest1);

        await Assert.That(result1).IsSameReferenceAs(dest1);
        await Assert.That(dest1.Name).IsEqualTo("John Doe");
        await Assert.That(dest1.BirthInfo).IsNotNull();
        await Assert.That(dest1.BirthInfo.Age).IsEqualTo(35);
        await Assert.That(dest1.BirthInfo.Address).IsEqualTo("Lviv");
        await Assert.That(dest1.ContactInfo).IsEqualTo("john@example.com");

        // Test case 2: Source has null BirthInfo (negative condition is true)
        var dest2 = new DestDto();
        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "Jane Doe",
            BirthInfo = null,
            Email = "jane@example.com"
        };

        var result2 = Mapper.MapToDestDto1(sourceWithNullBirthInfo, dest2);

        await Assert.That(result2).IsSameReferenceAs(dest2);
        await Assert.That(dest2.Name).IsEqualTo("Jane Doe");
        await Assert.That(dest2.BirthInfo).IsNull();
        await Assert.That(dest2.ContactInfo).IsEqualTo("jane@example.com");
    }

    [Test]
    public async Task Should_Update_Existing_Destination_Objects_Correctly()
    {
        // Test that when destination already has BirthInfo, it updates the properties rather than replacing
        
        var dest = new DestDto
        {
            Name = "Old Name",
            BirthInfo = new BirthInfoDto { Age = 999, Address = "Old Address" },
            ContactInfo = "old@example.com"
        };

        var source = new SourceDto
        {
            Name = "New Name",
            BirthInfo = new BirthInfo { Age = 25, Address = "New Address" },
            Email = "new@example.com"
        };

        var existingBirthInfo = dest.BirthInfo; // Keep reference to check it's the same object
        
        var result = Mapper.MapToDestDto(source, dest);

        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("New Name");
        await Assert.That(dest.BirthInfo).IsSameReferenceAs(existingBirthInfo); // Same object reference
        await Assert.That(dest.BirthInfo.Age).IsEqualTo(25); // But properties updated
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("New Address");
        await Assert.That(dest.ContactInfo).IsEqualTo("new@example.com");
    }

    [Test]
    public async Task Should_Create_New_Destination_Objects_When_Null()
    {
        // Test that when destination has null BirthInfo, it creates a new one
        
        var dest = new DestDto
        {
            Name = "Old Name",
            BirthInfo = null,
            ContactInfo = "old@example.com"
        };

        var source = new SourceDto
        {
            Name = "New Name",
            BirthInfo = new BirthInfo { Age = 25, Address = "New Address" },
            Email = "new@example.com"
        };
        
        var result = Mapper.MapToDestDto(source, dest);

        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("New Name");
        await Assert.That(dest.BirthInfo).IsNotNull(); // New object created
        await Assert.That(dest.BirthInfo.Age).IsEqualTo(25);
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("New Address");
        await Assert.That(dest.ContactInfo).IsEqualTo("new@example.com");
    }
}