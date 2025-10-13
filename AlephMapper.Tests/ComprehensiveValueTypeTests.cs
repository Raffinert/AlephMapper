using TUnit.Core;

namespace AlephMapper.Tests;

public class ComprehensiveValueTypeTests
{
    [Test]
    public async Task Should_Handle_Mixed_Value_And_Reference_Types_Correctly()
    {
        var source = new MixedTypeSource
        {
            IntValue = 42,
            StringValue = "Test String",
            BoolValue = true,
            DecimalValue = 123.45m,
            DateTimeValue = new DateTime(2023, 1, 15),
            NullableIntValue = 99,
            ReferenceObject = new TestObject { Name = "Reference", Value = 100 }
        };

        var dest = new MixedTypeDestination();

        // Call the generated update method
        MixedTypeMapper.MapToDestination(source, dest);

        // Verify all values were set correctly
        await Assert.That(dest.IntValue).IsEqualTo(42);
        await Assert.That(dest.StringValue).IsEqualTo("Test String");
        await Assert.That(dest.BoolValue).IsEqualTo(true);
        await Assert.That(dest.DecimalValue).IsEqualTo(123.45m);
        await Assert.That(dest.DateTimeValue).IsEqualTo(new DateTime(2023, 1, 15));
        await Assert.That(dest.NullableIntValue).IsEqualTo(99);
        await Assert.That(dest.ReferenceObject).IsNotNull();
        await Assert.That(dest.ReferenceObject.Name).IsEqualTo("Reference");
        await Assert.That(dest.ReferenceObject.Value).IsEqualTo(100);
    }

    [Test]
    public async Task Should_Handle_Null_Reference_Types_Correctly()
    {
        var source = new MixedTypeSource
        {
            IntValue = 42,
            StringValue = null,
            BoolValue = false,
            DecimalValue = 0m,
            DateTimeValue = default,
            NullableIntValue = null,
            ReferenceObject = null
        };

        var dest = new MixedTypeDestination();

        // Call the generated update method
        MixedTypeMapper.MapToDestination(source, dest);

        // Verify value types are set and reference types are null
        await Assert.That(dest.IntValue).IsEqualTo(42);
        await Assert.That(dest.StringValue).IsNull();
        await Assert.That(dest.BoolValue).IsEqualTo(false);
        await Assert.That(dest.DecimalValue).IsEqualTo(0m);
        await Assert.That(dest.DateTimeValue).IsEqualTo(default(DateTime));
        await Assert.That(dest.NullableIntValue).IsNull();
        await Assert.That(dest.ReferenceObject).IsNull();
    }
}

internal class MixedTypeSource
{
    public int IntValue { get; set; }
    public string StringValue { get; set; }
    public bool BoolValue { get; set; }
    public decimal DecimalValue { get; set; }
    public DateTime DateTimeValue { get; set; }
    public int? NullableIntValue { get; set; }
    public TestObject ReferenceObject { get; set; }
}

internal class MixedTypeDestination
{
    public int IntValue { get; set; }
    public string StringValue { get; set; }
    public bool BoolValue { get; set; }
    public decimal DecimalValue { get; set; }
    public DateTime DateTimeValue { get; set; }
    public int? NullableIntValue { get; set; }
    public TestObject ReferenceObject { get; set; }
}

internal class TestObject
{
    public string Name { get; set; }
    public int Value { get; set; }
}

[Expressive]
internal static partial class MixedTypeMapper
{
    [Updateable]
    public static MixedTypeDestination MapToDestination(MixedTypeSource source)
        => new MixedTypeDestination
        {
            IntValue = source.IntValue,
            StringValue = source.StringValue,
            BoolValue = source.BoolValue,
            DecimalValue = source.DecimalValue,
            DateTimeValue = source.DateTimeValue,
            NullableIntValue = source.NullableIntValue,
            ReferenceObject = source.ReferenceObject != null ? new TestObject
            {
                Name = source.ReferenceObject.Name,
                Value = source.ReferenceObject.Value
            } : null
        };
}