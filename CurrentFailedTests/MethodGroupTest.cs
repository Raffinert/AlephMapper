using AgileObjects.ReadableExpressions;
using AlephMapper;

namespace CurrentFailedTests;

public class MethodGroupTest
{
    [Test]
    public async Task MethodGroupShouldBeInlined()
    {
        // Act
        var actual = SimpleObjectMapper.MapToDtoExpression().ToReadableString();

        // Assert
        var expected = """
                        so => new SimpleDto
                        {
                            Attributes = so.Attributes.Select(attr => attr.Name).ToList() ?? new List<string>()
                        }
                        """;
        await Assert.That(actual).IsEqualTo(expected);
    }
}

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
