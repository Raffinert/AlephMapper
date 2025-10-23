namespace AlephMapper.SimpleTests;

[Updatable]
public static partial class CollectionUpdateMapper
{
    // Base mapping method used by the generator to create an Updatable overload
    public static CollectionDto Map(CollectionSource source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Tags = source.Tags // Collections should be skipped in generated Updatable method
    };
}

public class CollectionSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class CollectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

