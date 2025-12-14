using AlephMapper;

namespace Tests;

[Expressive]
public static partial class SampleMapper
{
    public static string ProjectName(SampleSource source) => source.Name;
}

public class SampleSource
{
    public string Name { get; set; } = string.Empty;
}