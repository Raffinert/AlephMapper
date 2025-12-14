namespace AlephMapper.Tests;

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