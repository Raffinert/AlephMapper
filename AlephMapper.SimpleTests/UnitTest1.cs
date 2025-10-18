namespace AlephMapper.SimpleTests;

public class UnitTest1
{
    [Test]
    public async Task Complex_DTO_Expression_Should_Work()
    {
        // Arrange & Act
        var simpleDtoExpression = SimpleEmployeeMapper.MapToSimpleDtoExpression();

        // Assert
        await Assert.That(simpleDtoExpression).IsNotNull();
    }
}