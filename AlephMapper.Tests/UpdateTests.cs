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
}