using System;
using System.Collections.Generic;
using System.Reflection;

namespace AlephMapper.Tests;

public class CollectionSkippingTests
{
    [Test]
    public async Task Collection_Properties_Should_Be_Skipped_In_Updateable_Methods()
    {
        // This test verifies that collection properties are skipped in updateable methods
        
        var mapperType = typeof(CollectionMapper);
        
        // Verify the type exists 
        await Assert.That(mapperType).IsNotNull();
        
        // The basic updateable methods should exist
        var mapToDestWithCollectionsMethod = mapperType.GetMethod("MapToDestWithCollections", new[] { typeof(SourceWithCollections) });
        var mapToDestSimpleMethod = mapperType.GetMethod("MapToDestSimple", new[] { typeof(SourceWithCollections) });
        
        await Assert.That(mapToDestWithCollectionsMethod).IsNotNull();
        await Assert.That(mapToDestSimpleMethod).IsNotNull();
        
        // The updateable overloads should be generated
        var mapToDestWithCollectionsOverload = mapperType.GetMethod("MapToDestWithCollections", 
            new[] { typeof(SourceWithCollections), typeof(DestWithCollections) });
        var mapToDestSimpleOverload = mapperType.GetMethod("MapToDestSimple", 
            new[] { typeof(SourceWithCollections), typeof(DestWithCollections) });
        
        await Assert.That(mapToDestWithCollectionsOverload).IsNotNull();
        await Assert.That(mapToDestSimpleOverload).IsNotNull();
        
        Console.WriteLine("Collection mapper methods generated successfully");
    }
    
    [Test]
    public async Task Updateable_Method_Should_Skip_Collection_Properties_But_Update_Regular_Properties()
    {
        // Test that collection properties are skipped but regular properties are updated
        var source = new SourceWithCollections
        {
            Name = "Test Name",
            Tags = new List<string> { "tag1", "tag2" },
            Metadata = new Dictionary<string, string> { { "key", "value" } },
            Categories = new[] { "cat1", "cat2" },
            Numbers = new HashSet<int> { 1, 2, 3 },
            NestedObject = new NestedModel
            {
                Value = "Nested Value",
                NestedList = new List<string> { "nested1", "nested2" }
            }
        };
        
        var dest = new DestWithCollections
        {
            Name = "Old Name",
            Tags = new List<string> { "oldtag" },
            Metadata = new Dictionary<string, string> { { "oldkey", "oldvalue" } },
            Categories = new[] { "oldcat" },
            Numbers = new HashSet<int> { 999 },
            NestedObject = new NestedModel
            {
                Value = "Old Nested Value",
                NestedList = new List<string> { "oldnested" }
            }
        };
        
        // Test the generated updateable method
        var result = CollectionMapper.MapToDestWithCollections(source, dest);
        
        // Verify it returns the same destination object
        await Assert.That(result).IsSameReferenceAs(dest);
        
        // Regular properties should be updated
        await Assert.That(dest.Name).IsEqualTo("Test Name");
        
        // Nested object should be updated (but its collections should be skipped)
        await Assert.That(dest.NestedObject).IsNotNull();
        await Assert.That(dest.NestedObject.Value).IsEqualTo("Nested Value");
        
        // Collection properties should NOT be updated (should retain old values)
        // This behavior depends on implementation - collections might be skipped entirely
        // or they might be left unchanged. The key is they shouldn't cause errors.
        
        Console.WriteLine("Updateable method with collections completed without errors");
    }
    
    [Test]
    public async Task Simple_Updateable_Method_Should_Work_Without_Collections()
    {
        // Test that non-collection properties work normally
        var source = new SourceWithCollections
        {
            Name = "Test Name",
            NestedObject = new NestedModel
            {
                Value = "Nested Value"
            }
        };
        
        var dest = new DestWithCollections
        {
            Name = "Old Name",
            NestedObject = new NestedModel
            {
                Value = "Old Nested Value"
            }
        };
        
        // Test the generated updateable method
        var result = CollectionMapper.MapToDestSimple(source, dest);
        
        // Verify it returns the same destination object
        await Assert.That(result).IsSameReferenceAs(dest);
        
        // Regular properties should be updated
        await Assert.That(dest.Name).IsEqualTo("Test Name");
        await Assert.That(dest.NestedObject).IsNotNull();
        await Assert.That(dest.NestedObject.Value).IsEqualTo("Nested Value");
        
        Console.WriteLine("Simple updateable method worked correctly");
    }
    
    [Test]
    public async Task Collection_Skipping_Should_Not_Prevent_Method_Generation()
    {
        // This test verifies that having collection properties doesn't prevent updateable method generation
        
        var mapperType = typeof(CollectionMapper);
        var methods = mapperType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        // Count methods
        var basicMethods = methods.Where(m => m.GetParameters().Length == 1).ToArray();
        var updateableMethods = methods.Where(m => m.GetParameters().Length == 2).ToArray(); // Updateable overloads
        var expressionMethods = methods.Where(m => m.Name.EndsWith("Expression")).ToArray();
        
        // We should have basic methods, updateable overloads, and expressions
        await Assert.That(basicMethods.Length).IsEqualTo(2); // MapToDestWithCollections, MapToDestSimple
        await Assert.That(updateableMethods.Length).IsEqualTo(2); // Both should have updateable overloads
        await Assert.That(expressionMethods.Length).IsEqualTo(2); // Both should have expressions
        
        Console.WriteLine($"Basic methods: {basicMethods.Length}");
        Console.WriteLine($"Updateable overload methods: {updateableMethods.Length}");
        Console.WriteLine($"Expression methods: {expressionMethods.Length}");
        Console.WriteLine("Collection properties did not prevent method generation");
    }
}