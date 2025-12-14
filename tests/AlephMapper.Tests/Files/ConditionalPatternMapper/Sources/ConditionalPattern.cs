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