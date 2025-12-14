namespace AlephMapper.Tests;

// Simple test types
internal struct SimpleValueTypeSource
{
    public int Value { get; set; }
    public string Name { get; set; }
}

internal class SimpleReferenceTypeDestination
{
    public int Value { get; set; }
    public string Name { get; set; }
}

[Expressive]
internal static partial class SimpleValueToReferenceMapper
{
    [Updatable]
    public static SimpleReferenceTypeDestination Map(SimpleValueTypeSource source) => new SimpleReferenceTypeDestination
    {
        Value = source.Value,
        Name = source.Name
    };
}

// Complex property test types
internal struct ComplexPropertyTestSource
{
    public NestedValueType NestedStruct { get; set; }
}

internal struct NestedValueType
{
    public int InnerValue { get; set; }
    public DeeplyNestedValueType InnerStruct { get; set; }
}

internal struct DeeplyNestedValueType
{
    public string DeepValue { get; set; }
}

internal class ComplexPropertyTestDestination
{
    public NestedReferenceType NestedClass { get; set; }
}

internal class NestedReferenceType
{
    public int InnerValue { get; set; }
    public DeeplyNestedReferenceType InnerClass { get; set; }
}

internal class DeeplyNestedReferenceType
{
    public string DeepValue { get; set; }
}

[Expressive]
internal static partial class ComplexPropertyMapper
{
    [Updatable]
    public static ComplexPropertyTestDestination Map(ComplexPropertyTestSource source) => new ComplexPropertyTestDestination
    {
        NestedClass = new NestedReferenceType
        {
            InnerValue = source.NestedStruct.InnerValue,
            InnerClass = new DeeplyNestedReferenceType
            {
                DeepValue = source.NestedStruct.InnerStruct.DeepValue
            }
        }
    };
}

// Edge case test types - designed to test the boundary conditions
internal struct EdgeCaseValueTypeSource
{
    public int SimpleValue { get; set; }
    public EdgeCaseValueTypeStruct ComplexValue { get; set; }
}

internal struct EdgeCaseValueTypeStruct
{
    public int StructValue { get; set; }
    public NestedEdgeCaseStruct NestedStruct { get; set; }
}

internal struct NestedEdgeCaseStruct
{
    public string Value { get; set; }
}

internal class EdgeCaseReferenceTypeDestination
{
    public int SimpleValue { get; set; }
    public EdgeCaseReferenceTypeClass ComplexValue { get; set; }
}

internal class EdgeCaseReferenceTypeClass
{
    public int StructValue { get; set; }
    public NestedEdgeCaseClass NestedClass { get; set; }
}

internal class NestedEdgeCaseClass
{
    public string Value { get; set; }
}

[Expressive]
internal static partial class EdgeCaseMapper
{
    [Updatable]
    public static EdgeCaseReferenceTypeDestination Map(EdgeCaseValueTypeSource source) => new EdgeCaseReferenceTypeDestination
    {
        SimpleValue = source.SimpleValue,
        ComplexValue = new EdgeCaseReferenceTypeClass
        {
            StructValue = source.ComplexValue.StructValue,
            NestedClass = new NestedEdgeCaseClass
            {
                Value = source.ComplexValue.NestedStruct.Value
            }
        }
    };
}