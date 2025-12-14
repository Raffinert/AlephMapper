namespace AlephMapper.Tests;

public class CircularPropsSource
{
    public string Name { get; set; } = string.Empty;
}

public class TypeA
{
    public string Name { get; set; } = string.Empty;
    public TypeB? B { get; set; }
}

public class TypeB
{
    public TypeA? A { get; set; }
}