namespace AlephMapper.Tests;

// Mapper with intentional circular references to test detection
[Expressive]
internal static partial class CircularMapper
{
    // Simple direct circular reference - this method calls itself
    public static string DirectCircular(CircularTestModel source) => 
        DirectCircular(source); // This should be detected as direct circular reference
    
    // Simple indirect circular reference - A calls B, B calls A
    public static CircularDto MapToDto(CircularTestModel source) => new CircularDto
    {
        ProcessedValue = MapToOtherDto(source).Value // Calls MapToOtherDto
    };

    public static OtherCircularDto MapToOtherDto(CircularTestModel source) => new OtherCircularDto
    {
        Value = MapToDto(source).ProcessedValue // Calls MapToDto - creates cycle
    };
    
    // Updatable method with circular reference
    [Updatable]
    public static CircularDto UpdateCircularDto(CircularTestModel source) => new CircularDto
    {
        ProcessedValue = MapToOtherDto(source).Value // Calls MapToOtherDto which has circular reference
    };
    
    // Another Updatable method with circular reference
    [Updatable] 
    public static OtherCircularDto UpdateOtherCircularDto(CircularTestModel source) => new OtherCircularDto
    {
        Value = MapToDto(source).ProcessedValue // Calls MapToDto which has circular reference
    };
    
    // A helper method without circular reference for comparison
    public static string ProcessValue(CircularTestModel source) => source?.Value?.ToUpper() ?? "";
    
    // Updatable method without circular reference for comparison
    [Updatable]
    public static CircularDto UpdateSimpleDto(CircularTestModel source) => new CircularDto
    {
        ProcessedValue = source?.Value?.ToUpper() ?? "" // Direct assignment without method call
    };
}