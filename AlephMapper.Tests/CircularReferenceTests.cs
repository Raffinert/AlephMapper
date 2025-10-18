using System;
using System.Reflection;

namespace AlephMapper.Tests;

public class CircularReferenceTests
{
    [Test]
    public async Task Circular_References_Should_Be_Detected_And_Expression_Methods_Skipped()
    {
        // This test verifies that circular references in mapping methods are detected
        // and the expression methods are NOT generated for those methods
        
        var mapperType = typeof(CircularMapper);
        
        // Verify the type exists 
        await Assert.That(mapperType).IsNotNull();
        
        // The basic methods should exist
        var mapToDtoMethod = mapperType.GetMethod("MapToDto", new[] { typeof(CircularTestModel) });
        var mapToOtherDtoMethod = mapperType.GetMethod("MapToOtherDto", new[] { typeof(CircularTestModel) });
        var directCircularMethod = mapperType.GetMethod("DirectCircular", new[] { typeof(CircularTestModel) });
        var processValueMethod = mapperType.GetMethod("ProcessValue", new[] { typeof(CircularTestModel) });
        var updateSimpleDtoMethod = mapperType.GetMethod("UpdateSimpleDto", new[] { typeof(CircularTestModel) });
        
        await Assert.That(mapToDtoMethod).IsNotNull();
        await Assert.That(mapToOtherDtoMethod).IsNotNull();
        await Assert.That(directCircularMethod).IsNotNull();
        await Assert.That(processValueMethod).IsNotNull();
        await Assert.That(updateSimpleDtoMethod).IsNotNull();
        
        // The expression methods should NOT be generated for circular methods
        var mapToDtoExpressionMethod = mapperType.GetMethod("MapToDtoExpression");
        var mapToOtherDtoExpressionMethod = mapperType.GetMethod("MapToOtherDtoExpression");
        var directCircularExpressionMethod = mapperType.GetMethod("DirectCircularExpression");
        
        await Assert.That(mapToDtoExpressionMethod).IsNull();
        await Assert.That(mapToOtherDtoExpressionMethod).IsNull();
        await Assert.That(directCircularExpressionMethod).IsNull();
        
        // But expression methods SHOULD be generated for non-circular methods
        var processValueExpressionMethod = mapperType.GetMethod("ProcessValueExpression");
        var updateSimpleDtoExpressionMethod = mapperType.GetMethod("UpdateSimpleDtoExpression");
        await Assert.That(processValueExpressionMethod).IsNotNull();
        await Assert.That(updateSimpleDtoExpressionMethod).IsNotNull();
        
        Console.WriteLine("Circular reference detection working correctly:");
        Console.WriteLine("- Circular methods: no expression methods generated");
        Console.WriteLine("- Non-circular methods: expression methods generated");
    }
    
    [Test]
    public async Task Circular_References_Should_Be_Detected_And_Updateable_Methods_Skipped()
    {
        // This test verifies that circular references in updateable methods are detected
        // and the updateable methods are NOT generated for those methods
        
        var mapperType = typeof(CircularMapper);
        
        // The basic updateable methods should exist (these are the original method signatures)
        var updateCircularDtoMethod = mapperType.GetMethod("UpdateCircularDto", new[] { typeof(CircularTestModel) });
        var updateOtherCircularDtoMethod = mapperType.GetMethod("UpdateOtherCircularDto", new[] { typeof(CircularTestModel) });
        var updateSimpleDtoMethod = mapperType.GetMethod("UpdateSimpleDto", new[] { typeof(CircularTestModel) });
        
        await Assert.That(updateCircularDtoMethod).IsNotNull();
        await Assert.That(updateOtherCircularDtoMethod).IsNotNull();
        await Assert.That(updateSimpleDtoMethod).IsNotNull();
        
        // The generated updateable overloads should NOT be created for circular methods
        // Generated updateable methods have signature: Method(Source source, Target target)
        var updateCircularDtoOverload = mapperType.GetMethod("UpdateCircularDto", 
            new[] { typeof(CircularTestModel), typeof(CircularDto) });
        var updateOtherCircularDtoOverload = mapperType.GetMethod("UpdateOtherCircularDto", 
            new[] { typeof(CircularTestModel), typeof(OtherCircularDto) });
        
        await Assert.That(updateCircularDtoOverload).IsNull();
        await Assert.That(updateOtherCircularDtoOverload).IsNull();
        
        // But updateable overloads SHOULD be generated for non-circular methods
        var updateSimpleDtoOverload = mapperType.GetMethod("UpdateSimpleDto", 
            new[] { typeof(CircularTestModel), typeof(CircularDto) });
        await Assert.That(updateSimpleDtoOverload).IsNotNull();
        
        Console.WriteLine("Updateable circular reference detection working correctly:");
        Console.WriteLine("- Circular updateable methods: no overloads generated");
        Console.WriteLine("- Non-circular updateable methods: overloads generated");
    }
    
    [Test]
    public async Task Non_Circular_Method_Should_Have_Working_Expression()
    {
        // Test that the non-circular method works correctly
        var source = new CircularTestModel { Value = "test" };
        
        // Test the regular method
        var result = CircularMapper.ProcessValue(source);
        await Assert.That(result).IsEqualTo("TEST");
        
        // Test the generated expression method
        var expression = CircularMapper.ProcessValueExpression();
        var compiled = expression.Compile();
        var expressionResult = compiled(source);
        
        await Assert.That(expressionResult).IsEqualTo("TEST");
        
        // They should produce the same result
        await Assert.That(result).IsEqualTo(expressionResult);
    }
    
    [Test]
    public async Task Non_Circular_Updateable_Method_Should_Work()
    {
        // Test that the non-circular updateable method works correctly
        var source = new CircularTestModel { Value = "test" };
        var dest = new CircularDto();
        
        // Test the generated updateable method
        var result = CircularMapper.UpdateSimpleDto(source, dest);
        
        // Verify it returns the same destination object
        await Assert.That(result).IsSameReferenceAs(dest);
        
        // Verify the destination was updated correctly
        await Assert.That(dest.ProcessedValue).IsEqualTo("TEST");
        
        Console.WriteLine("Non-circular updateable method working correctly");
    }
    
    [Test] 
    public async Task Circular_Methods_Should_Not_Cause_Stack_Overflow_During_Generation()
    {
        // This test verifies that the presence of circular methods doesn't break the generation process
        // The fact that we can get here and the type exists means circular reference detection worked
        
        var mapperType = typeof(CircularMapper);
        var methods = mapperType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        // Count methods - should have basic methods but only expression/updateable methods for non-circular ones
        var basicMethods = methods.Where(m => !m.Name.EndsWith("Expression") && m.GetParameters().Length == 1).ToArray();
        var expressionMethods = methods.Where(m => m.Name.EndsWith("Expression")).ToArray();
        var updateableMethods = methods.Where(m => m.GetParameters().Length == 2).ToArray(); // Updateable overloads
        
        await Assert.That(basicMethods.Length).IsEqualTo(7); // All basic methods should exist
        await Assert.That(expressionMethods.Length).IsEqualTo(2); // ProcessValueExpression and UpdateSimpleDtoExpression
        await Assert.That(updateableMethods.Length).IsEqualTo(1); // Only UpdateSimpleDto overload
        
        Console.WriteLine($"Basic methods: {basicMethods.Length}");
        Console.WriteLine($"Expression methods: {expressionMethods.Length}");
        Console.WriteLine($"Updateable overload methods: {updateableMethods.Length}");
        Console.WriteLine("Code generation completed successfully without infinite loops");
    }
}

// Test models for circular reference testing
public class CircularTestModel
{
    public string Value { get; set; }
}

public class CircularDto
{
    public string ProcessedValue { get; set; }
}

public class OtherCircularDto
{
    public string Value { get; set; }
}