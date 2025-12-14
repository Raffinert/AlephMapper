using TUnit.Core;

namespace AlephMapper.Tests;

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
    [Updatable]
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