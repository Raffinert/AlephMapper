namespace AlephMapper.Tests;

// Test models for all conditional patterns
public class SourceModel
{
    public string? Name { get; set; }
    public int? Value { get; set; }
    public NestedSource? Nested { get; set; }
}

public class NestedSource
{
    public string? Content { get; set; }
    public int Number { get; set; }
}

public class DestModel
{
    public string? Name { get; set; }
    public int? Value { get; set; }
    public NestedDest? Nested { get; set; }
}

public class NestedDest
{
    public string? Content { get; set; }
    public int Number { get; set; }
}

// Mappers for testing all conditional patterns
public static partial class ConditionalPatternMapper
{
    // Pattern 1: Both sides object creation
    [Updatable]
    public static DestModel BothSidesObjects(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Value == null ? 
            new NestedDest { Content = "Default", Number = 0 } : 
            new NestedDest { Content = source.Name, Number = source.Value.Value }
    };

    // Pattern 4: Existing pattern (condition ? object : null)
    [Updatable]
    public static DestModel ObjectThenNull(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Name != null ? 
            new NestedDest { Content = source.Name, Number = 42 } : 
            null
    };

    // Pattern 5: Existing pattern (condition ? null : object)
    [Updatable]
    public static DestModel NullThenObject(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Name == null ? 
            null : 
            new NestedDest { Content = source.Name, Number = 42 }
    };

    // Pattern 6: Complex nested both sides object creation
    [Updatable]
    public static DestModel NestedBothSides(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Nested?.Content == null ? 
            new NestedDest { Content = "Fallback", Number = -1 } : 
            new NestedDest { Content = source.Nested.Content, Number = source.Nested.Number }
    };

    // Pattern 7: Existing pattern (condition ? object : throw)
    [Updatable]
    public static DestModel ObjectThenThrow(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Name != null ?
            new NestedDest { Content = source.Name, Number = 42 } :
            throw new ArgumentNullException(nameof(source.Name))
    };

    // Pattern 8: Existing pattern (condition ? throw : object)
    [Updatable]
    public static DestModel ThrowThenObject(SourceModel source) => new DestModel
    {
        Name = source.Name,
        Nested = source.Name == null ?
            throw new ArgumentNullException(nameof(source.Name)) :
            new NestedDest { Content = source.Name, Number = 42 }
    };
}

public class ConditionalPatternTests
{
    [Test]
    public async Task BothSidesObjects_Should_Create_Correct_Object_Based_On_Condition()
    {
        // Test true branch
        var source = new SourceModel { Name = "Test", Value = 123 };
        var dest = new DestModel();

        ConditionalPatternMapper.BothSidesObjects(source, dest);

        await Assert.That(dest.Name).IsEqualTo("Test");
        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("Test");
        await Assert.That(dest.Nested.Number).IsEqualTo(123);

        // Test false branch
        var source2 = new SourceModel { Name = "Test2", Value = null };
        var dest2 = new DestModel();

        ConditionalPatternMapper.BothSidesObjects(source2, dest2);

        await Assert.That(dest2.Name).IsEqualTo("Test2");
        await Assert.That(dest2.Nested).IsNotNull();
        await Assert.That(dest2.Nested!.Content).IsEqualTo("Default");
        await Assert.That(dest2.Nested.Number).IsEqualTo(0);
    }

    [Test]
    public async Task BothSidesObjects_Should_Update_Existing_Object()
    {
        // Arrange - destination already has nested object
        var existingNested = new NestedDest { Content = "Old", Number = 999 };
        var dest = new DestModel { Name = "Old", Nested = existingNested };

        var source = new SourceModel { Name = "New", Value = 456 };

        // Act
        ConditionalPatternMapper.BothSidesObjects(source, dest);

        // Assert - existing object should be reused and updated
        await Assert.That(dest.Nested).IsSameReferenceAs(existingNested);
        await Assert.That(dest.Nested.Content).IsEqualTo("New");
        await Assert.That(dest.Nested.Number).IsEqualTo(456);
    }

    [Test]
    public async Task ObjectThenNull_Should_Work_As_Before()
    {
        // Test object creation
        var source = new SourceModel { Name = "Valid" };
        var dest = new DestModel();

        ConditionalPatternMapper.ObjectThenNull(source, dest);

        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("Valid");

        // Test null assignment
        var source2 = new SourceModel { Name = null };
        var dest2 = new DestModel { Nested = new NestedDest() };

        ConditionalPatternMapper.ObjectThenNull(source2, dest2);

        await Assert.That(dest2.Nested).IsNull();
    }

    [Test]
    public async Task NullThenObject_Should_Work_As_Before()
    {
        // Test null assignment
        var source = new SourceModel { Name = null };
        var dest = new DestModel { Nested = new NestedDest() };

        ConditionalPatternMapper.NullThenObject(source, dest);

        await Assert.That(dest.Nested).IsNull();

        // Test object creation
        var source2 = new SourceModel { Name = "Valid" };
        var dest2 = new DestModel();

        ConditionalPatternMapper.NullThenObject(source2, dest2);

        await Assert.That(dest2.Nested).IsNotNull();
        await Assert.That(dest2.Nested!.Content).IsEqualTo("Valid");
    }

    [Test]
    public async Task NestedBothSides_Should_Handle_Complex_Nested_Scenarios()
    {
        // Test true branch (nested content is null)
        var source = new SourceModel 
        { 
            Name = "Test", 
            Nested = new NestedSource { Content = null, Number = 100 }
        };
        var dest = new DestModel();

        ConditionalPatternMapper.NestedBothSides(source, dest);

        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("Fallback");
        await Assert.That(dest.Nested.Number).IsEqualTo(-1);

        // Test false branch (nested content is not null)
        var source2 = new SourceModel 
        { 
            Name = "Test2", 
            Nested = new NestedSource { Content = "Valid", Number = 200 }
        };
        var dest2 = new DestModel();

        ConditionalPatternMapper.NestedBothSides(source2, dest2);

        await Assert.That(dest2.Nested).IsNotNull();
        await Assert.That(dest2.Nested!.Content).IsEqualTo("Valid");
        await Assert.That(dest2.Nested.Number).IsEqualTo(200);
    }

    // Tests for ObjectThenThrow method
    [Test]
    public async Task ObjectThenThrow_Should_Create_Object_When_Condition_True()
    {
        // Arrange
        var source = new SourceModel { Name = "ValidName" };
        var dest = new DestModel();

        // Act
        ConditionalPatternMapper.ObjectThenThrow(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("ValidName");
        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("ValidName");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ObjectThenThrow_Should_Throw_When_Condition_False()
    {
        // Arrange
        var source = new SourceModel { Name = null };
        var dest = new DestModel();

        // Act & Assert
        await Assert.That(() => ConditionalPatternMapper.ObjectThenThrow(source, dest))
            .Throws<ArgumentNullException>()
            .WithParameterName("Name");
    }

    [Test]
    public async Task ObjectThenThrow_Should_Update_Existing_Object_When_Valid()
    {
        // Arrange - destination already has nested object
        var existingNested = new NestedDest { Content = "OldContent", Number = 999 };
        var dest = new DestModel { Name = "OldName", Nested = existingNested };
        var source = new SourceModel { Name = "UpdatedName" };

        // Act
        ConditionalPatternMapper.ObjectThenThrow(source, dest);

        // Assert - existing object should be reused and updated
        await Assert.That(dest.Name).IsEqualTo("UpdatedName");
        await Assert.That(dest.Nested).IsSameReferenceAs(existingNested);
        await Assert.That(dest.Nested.Content).IsEqualTo("UpdatedName");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ObjectThenThrow_Should_Not_Modify_Dest_When_Exception_Thrown()
    {
        // Arrange - destination has existing data
        var existingNested = new NestedDest { Content = "OriginalContent", Number = 123 };
        var dest = new DestModel { Name = "OriginalName", Nested = existingNested };
        var source = new SourceModel { Name = null };

        // Act & Assert
        await Assert.That(() => ConditionalPatternMapper.ObjectThenThrow(source, dest))
            .Throws<ArgumentNullException>();

        // Verify destination wasn't modified (Name would be set before the exception)
        await Assert.That(dest.Name).IsNull(); // Name gets set from source.Name
        await Assert.That(dest.Nested).IsSameReferenceAs(existingNested);
        await Assert.That(dest.Nested.Content).IsEqualTo("OriginalContent");
        await Assert.That(dest.Nested.Number).IsEqualTo(123);
    }

    // Tests for ThrowThenObject method
    [Test]
    public async Task ThrowThenObject_Should_Create_Object_When_Condition_False()
    {
        // Arrange
        var source = new SourceModel { Name = "ValidName" };
        var dest = new DestModel();

        // Act
        ConditionalPatternMapper.ThrowThenObject(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("ValidName");
        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("ValidName");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ThrowThenObject_Should_Throw_When_Condition_True()
    {
        // Arrange
        var source = new SourceModel { Name = null };
        var dest = new DestModel();

        // Act & Assert
        await Assert.That(() => ConditionalPatternMapper.ThrowThenObject(source, dest))
            .Throws<ArgumentNullException>()
            .WithParameterName("Name");
    }

    [Test]
    public async Task ThrowThenObject_Should_Update_Existing_Object_When_Valid()
    {
        // Arrange - destination already has nested object
        var existingNested = new NestedDest { Content = "OldContent", Number = 999 };
        var dest = new DestModel { Name = "OldName", Nested = existingNested };
        var source = new SourceModel { Name = "UpdatedName" };

        // Act
        ConditionalPatternMapper.ThrowThenObject(source, dest);

        // Assert - existing object should be reused and updated
        await Assert.That(dest.Name).IsEqualTo("UpdatedName");
        await Assert.That(dest.Nested).IsSameReferenceAs(existingNested);
        await Assert.That(dest.Nested.Content).IsEqualTo("UpdatedName");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ThrowThenObject_Should_Not_Modify_Dest_When_Exception_Thrown()
    {
        // Arrange - destination has existing data
        var existingNested = new NestedDest { Content = "OriginalContent", Number = 123 };
        var dest = new DestModel { Name = "OriginalName", Nested = existingNested };
        var source = new SourceModel { Name = null };

        // Act & Assert
        await Assert.That(() => ConditionalPatternMapper.ThrowThenObject(source, dest))
            .Throws<ArgumentNullException>();

        // Verify destination wasn't modified (Name would be set before the exception)
        await Assert.That(dest.Name).IsNull(); // Name gets set from source.Name
        await Assert.That(dest.Nested).IsSameReferenceAs(existingNested);
        await Assert.That(dest.Nested.Content).IsEqualTo("OriginalContent");
        await Assert.That(dest.Nested.Number).IsEqualTo(123);
    }

    // Edge case tests for both methods
    [Test]
    public async Task ObjectThenThrow_Should_Handle_Empty_String_As_Valid()
    {
        // Arrange
        var source = new SourceModel { Name = "" }; // Empty string is not null
        var dest = new DestModel();

        // Act
        ConditionalPatternMapper.ObjectThenThrow(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("");
        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ThrowThenObject_Should_Handle_Empty_String_As_Valid()
    {
        // Arrange
        var source = new SourceModel { Name = "" }; // Empty string is not null
        var dest = new DestModel();

        // Act
        ConditionalPatternMapper.ThrowThenObject(source, dest);

        // Assert
        await Assert.That(dest.Name).IsEqualTo("");
        await Assert.That(dest.Nested).IsNotNull();
        await Assert.That(dest.Nested!.Content).IsEqualTo("");
        await Assert.That(dest.Nested.Number).IsEqualTo(42);
    }

    [Test]
    public async Task ObjectThenThrow_And_ThrowThenObject_Should_Have_Symmetric_Behavior()
    {
        // Both methods should behave the same for valid inputs, just with reversed conditions
        
        // Test with valid name
        var validSource = new SourceModel { Name = "TestName" };
        var dest1 = new DestModel();
        var dest2 = new DestModel();

        ConditionalPatternMapper.ObjectThenThrow(validSource, dest1);
        ConditionalPatternMapper.ThrowThenObject(validSource, dest2);

        // Both should produce the same result for valid input
        await Assert.That(dest1.Name).IsEqualTo(dest2.Name);
        await Assert.That(dest1.Nested?.Content).IsEqualTo(dest2.Nested?.Content);
        await Assert.That(dest1.Nested?.Number).IsEqualTo(dest2.Nested?.Number);

        // Test with null name - both should throw
        var nullSource = new SourceModel { Name = null };
        var dest3 = new DestModel();
        var dest4 = new DestModel();

        await Assert.That(() => ConditionalPatternMapper.ObjectThenThrow(nullSource, dest3))
            .Throws<ArgumentNullException>();
        await Assert.That(() => ConditionalPatternMapper.ThrowThenObject(nullSource, dest4))
            .Throws<ArgumentNullException>();
    }
}