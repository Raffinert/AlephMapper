using AgileObjects.ReadableExpressions;

namespace AlephMapper.Tests;

internal class SimpleObject
{
    public List<SimpleAttribute>? Attributes { get; set; }
}

internal class SimpleAttribute
{
    public string Name { get; set; }
}

internal class SimpleDto
{
    public List<string> Attributes { get; set; }
}

internal static partial class SimpleObjectMapper
{
    [Expressive]
    public static SimpleDto MapToDto(SimpleObject so) => new SimpleDto
    {
        Attributes = so.Attributes?.Select(MapFromAttribute).ToList() ?? []
    };

    private static SimpleAttribute MapToAttribute(string name) => new SimpleAttribute
    {
        Name = name
    };

    private static string MapFromAttribute(SimpleAttribute attr) => attr.Name;
}
