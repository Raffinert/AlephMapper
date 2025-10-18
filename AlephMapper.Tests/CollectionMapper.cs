using System.Collections.Generic;

namespace AlephMapper.Tests;

// Test models for collection property testing
public class SourceWithCollections
{
    public string Name { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string[] Categories { get; set; }
    public HashSet<int> Numbers { get; set; }
    public NestedModel NestedObject { get; set; }
}

public class DestWithCollections
{
    public string Name { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string[] Categories { get; set; }
    public HashSet<int> Numbers { get; set; }
    public NestedModel NestedObject { get; set; }
}

public class NestedModel
{
    public string Value { get; set; }
    public List<string> NestedList { get; set; }
}

// Mapper with collection properties to test skipping behavior
[Expressive]
internal static partial class CollectionMapper
{
    [Updateable]
    public static DestWithCollections MapToDestWithCollections(SourceWithCollections source) => new DestWithCollections
    {
        Name = source.Name,
        Tags = source.Tags, // This should be skipped (List<string>)
        Categories = source.Categories, // This should be skipped (string[])
        NestedObject = source.NestedObject != null ? new NestedModel
        {
            Value = source.NestedObject.Value,
            NestedList = source.NestedObject.NestedList // This should be skipped (List<string>)
        } : null
    };

    [Updateable]
    public static DestWithCollections MapToDestSimple(SourceWithCollections source) => new DestWithCollections
    {
        Name = source.Name, // This should NOT be skipped (string is not treated as collection)
        NestedObject = source.NestedObject != null ? new NestedModel
        {
            Value = source.NestedObject.Value // This should NOT be skipped (simple property)
            // Note: No NestedList here, so no collection to skip
        } : null
    };
}