using AlephMapper;

namespace Tests;

[Updatable]
public static partial class SampleMapper
{
    public static Destination Map(Source source) => new Destination
    {
        Name = source.Name,
        Age = source.Age
    };
}

public class Source
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class Destination
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}