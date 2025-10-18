namespace AlephMapper.Tests;

public class ValueTypeUpdateTests
{
    [Test]
    public async Task Should_Handle_Value_Types_Correctly()
    {
        var source = new ValueTypeSource
        {
            Age = 25,
            IsActive = true,
            BirthDate = new DateTime(1998, 5, 15),
            Salary = 50000.50m,
            Height = 175.5,
            Name = "John Doe"
        };

        var dest = new ValueTypeDestination
        {
            Age = 0,
            IsActive = false,
            BirthDate = default,
            Salary = 0,
            Height = 0,
            Name = "Original Name"
        };

        // Use the single-parameter mapping method since updateable methods
        // are not generated for value types (they don't make sense)
        dest = ValueTypeMapper.MapToDestination(source);


        // Verify the values were updated correctly
        await Assert.That(dest.Age).IsEqualTo(25);
        await Assert.That(dest.IsActive).IsEqualTo(true);
        await Assert.That(dest.BirthDate).IsEqualTo(new DateTime(1998, 5, 15));
        await Assert.That(dest.Salary).IsEqualTo(50000.50m);
        await Assert.That(dest.Height).IsEqualTo(175.5);
        await Assert.That(dest.Name).IsEqualTo("John Doe");
    }
}