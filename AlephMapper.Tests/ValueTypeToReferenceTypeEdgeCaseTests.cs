using TUnit.Core;

namespace AlephMapper.Tests;

public class ValueTypeToReferenceTypeEdgeCaseTests
{
    [Test]
    public async Task Should_Handle_Null_Parameter_Correctly_For_Value_To_Reference_Mapping()
    {
        // Test the null check logic when mapping from value type to reference type
        // Source is value type (cannot be null), but destination is reference type (can be null)
            
        var source = new SimpleValueTypeSource { Value = 42, Name = "Test" };
            
        // This should work - source cannot be null, destination can be null but we pass non-null
        var destination = new SimpleReferenceTypeDestination();
        SimpleValueToReferenceMapper.Map(source, destination);
            
        await Assert.That(destination.Value).IsEqualTo(42);
        await Assert.That(destination.Name).IsEqualTo("Test");
            
        // Test with null destination - should return null (based on our null check logic)
        var result = SimpleValueToReferenceMapper.Map(source, null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Should_Generate_Correct_Null_Checks_For_Mixed_Types()
    {
        // This test verifies that the null check generation logic works correctly
        // when source is value type (no null check) and destination is reference type (null check)
            
        var source = new SimpleValueTypeSource { Value = 100, Name = "Source" };
        var destination = new SimpleReferenceTypeDestination { Value = 0, Name = "Original" };
            
        // This should call the generated updateable method with proper null checks
        var result = SimpleValueToReferenceMapper.Map(source, destination);
            
        await Assert.That(result).IsSameReferenceAs(destination);
        await Assert.That(destination.Value).IsEqualTo(100);
        await Assert.That(destination.Name).IsEqualTo("Source");
    }

    [Test] 
    public async Task Should_Handle_Complex_Property_Path_Assignments_Correctly()
    {
        // This test checks that complex property paths are handled correctly
        // especially when crossing value type to reference type boundaries
            
        var source = new ComplexPropertyTestSource
        {
            NestedStruct = new NestedValueType
            {
                InnerValue = 123,
                InnerStruct = new DeeplyNestedValueType
                {
                    DeepValue = "deep test"
                }
            }
        };

        var destination = new ComplexPropertyTestDestination();
        ComplexPropertyMapper.Map(source, destination);

        await Assert.That(destination.NestedClass).IsNotNull();
        await Assert.That(destination.NestedClass.InnerValue).IsEqualTo(123);
        await Assert.That(destination.NestedClass.InnerClass).IsNotNull();
        await Assert.That(destination.NestedClass.InnerClass.DeepValue).IsEqualTo("deep test");
    }

    [Test]
    public async Task Should_Detect_And_Handle_Value_Type_Property_Assignment_Issues()
    {
        // This test is designed to catch any issues with the IsValueTypePropertyAssignment
        // logic in EmitHelpers.cs when dealing with complex mappings
            
        var source = new EdgeCaseValueTypeSource
        {
            SimpleValue = 42,
            ComplexValue = new EdgeCaseValueTypeStruct
            {
                StructValue = 99,
                NestedStruct = new NestedEdgeCaseStruct
                {
                    Value = "nested"
                }
            }
        };

        var destination = new EdgeCaseReferenceTypeDestination();
        EdgeCaseMapper.Map(source, destination);

        await Assert.That(destination.SimpleValue).IsEqualTo(42);
        await Assert.That(destination.ComplexValue).IsNotNull();
        await Assert.That(destination.ComplexValue.StructValue).IsEqualTo(99);
        await Assert.That(destination.ComplexValue.NestedClass).IsNotNull();
        await Assert.That(destination.ComplexValue.NestedClass.Value).IsEqualTo("nested");
    }
}

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
    [Updateable]
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
    [Updateable]
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
    [Updateable]
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