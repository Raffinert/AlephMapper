namespace AlephMapper.Tests;

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