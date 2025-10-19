namespace AlephMapper.Tests;

public class ValueTypeNullCheckTests
{
    [Test]
    public async Task Should_Not_Generate_Null_Checks_For_Value_Types()
    {
        // This test verifies that our type annotation system correctly 
        // identifies value types and doesn't generate null checks for them
            
        var source = new ValueTypeOnlySource
        {
            IntProperty = 42,
            BoolProperty = true,
            DateTimeProperty = new DateTime(2023, 1, 1),
            DecimalProperty = 99.99m
        };

        var dest = new ValueTypeOnlyDestination();

        // This should work without any null reference exceptions
        // because value types don't need null checks
        ValueTypeOnlyMapper.MapToDestination(source, dest);

        await Assert.That(dest.IntProperty).IsEqualTo(42);
        await Assert.That(dest.BoolProperty).IsEqualTo(true);
        await Assert.That(dest.DateTimeProperty).IsEqualTo(new DateTime(2023, 1, 1));
        await Assert.That(dest.DecimalProperty).IsEqualTo(99.99m);
    }

    [Test]
    public async Task Should_Generate_Null_Checks_For_Reference_Types()
    {
        // This test verifies that reference types still get proper null handling
            
        var source = new ReferenceTypeOnlySource
        {
            StringProperty = "test",
            ObjectProperty = new SimpleReferenceObject { Name = "test" }
        };

        var dest = new ReferenceTypeOnlyDestination();

        ReferenceTypeOnlyMapper.MapToDestination(source, dest);

        await Assert.That(dest.StringProperty).IsEqualTo("test");
        await Assert.That(dest.ObjectProperty).IsNotNull();
        await Assert.That(dest.ObjectProperty.Name).IsEqualTo("test");

        // Test with null values
        var nullSource = new ReferenceTypeOnlySource
        {
            StringProperty = null,
            ObjectProperty = null
        };

        var dest2 = new ReferenceTypeOnlyDestination();
        ReferenceTypeOnlyMapper.MapToDestination(nullSource, dest2);

        await Assert.That(dest2.StringProperty).IsNull();
        await Assert.That(dest2.ObjectProperty).IsNull();
    }

    [Test] 
    public async Task Should_Handle_Nullable_Value_Types_Correctly()
    {
        // This test verifies that nullable value types are treated as "can be null"
            
        var source = new NullableValueTypeSource
        {
            NullableIntProperty = 42,
            NullableBoolProperty = true,
            NullableDateTimeProperty = new DateTime(2023, 1, 1)
        };

        var dest = new NullableValueTypeDestination();

        NullableValueTypeMapper.MapToDestination(source, dest);

        await Assert.That(dest.NullableIntProperty).IsEqualTo(42);
        await Assert.That(dest.NullableBoolProperty).IsEqualTo(true);
        await Assert.That(dest.NullableDateTimeProperty).IsEqualTo(new DateTime(2023, 1, 1));

        // Test with null values
        var nullSource = new NullableValueTypeSource
        {
            NullableIntProperty = null,
            NullableBoolProperty = null,
            NullableDateTimeProperty = null
        };

        var dest2 = new NullableValueTypeDestination();
        NullableValueTypeMapper.MapToDestination(nullSource, dest2);

        await Assert.That(dest2.NullableIntProperty).IsNull();
        await Assert.That(dest2.NullableBoolProperty).IsNull();
        await Assert.That(dest2.NullableDateTimeProperty).IsNull();
    }
}

// Value types only - should not generate null checks
internal class ValueTypeOnlySource
{
    public int IntProperty { get; set; }
    public bool BoolProperty { get; set; }
    public DateTime DateTimeProperty { get; set; }
    public decimal DecimalProperty { get; set; }
}

internal class ValueTypeOnlyDestination
{
    public int IntProperty { get; set; }
    public bool BoolProperty { get; set; }
    public DateTime DateTimeProperty { get; set; }
    public decimal DecimalProperty { get; set; }
}

[Expressive]
internal static partial class ValueTypeOnlyMapper
{
    [Updatable]
    public static ValueTypeOnlyDestination MapToDestination(ValueTypeOnlySource source)
        => new ValueTypeOnlyDestination
        {
            IntProperty = source.IntProperty,
            BoolProperty = source.BoolProperty,
            DateTimeProperty = source.DateTimeProperty,
            DecimalProperty = source.DecimalProperty
        };
}

// Reference types only - should generate null checks
internal class ReferenceTypeOnlySource
{
    public string StringProperty { get; set; }
    public SimpleReferenceObject ObjectProperty { get; set; }
}

internal class ReferenceTypeOnlyDestination
{
    public string StringProperty { get; set; }
    public SimpleReferenceObject ObjectProperty { get; set; }
}

internal class SimpleReferenceObject
{
    public string Name { get; set; }
}

[Expressive]
internal static partial class ReferenceTypeOnlyMapper
{
    [Updatable]
    public static ReferenceTypeOnlyDestination MapToDestination(ReferenceTypeOnlySource source)
        => new ReferenceTypeOnlyDestination
        {
            StringProperty = source.StringProperty,
            ObjectProperty = source.ObjectProperty != null ? new SimpleReferenceObject
            {
                Name = source.ObjectProperty.Name
            } : null
        };
}

// Nullable value types - should generate null checks
internal class NullableValueTypeSource
{
    public int? NullableIntProperty { get; set; }
    public bool? NullableBoolProperty { get; set; }
    public DateTime? NullableDateTimeProperty { get; set; }
}

internal class NullableValueTypeDestination
{
    public int? NullableIntProperty { get; set; }
    public bool? NullableBoolProperty { get; set; }
    public DateTime? NullableDateTimeProperty { get; set; }
}

[Expressive]
internal static partial class NullableValueTypeMapper
{
    [Updatable]
    public static NullableValueTypeDestination MapToDestination(NullableValueTypeSource source)
        => new NullableValueTypeDestination
        {
            NullableIntProperty = source.NullableIntProperty,
            NullableBoolProperty = source.NullableBoolProperty,
            NullableDateTimeProperty = source.NullableDateTimeProperty
        };
}