namespace AlephMapper.IntegrationTests;

// Models and mappers covering update-in-place semantics that previously threw,
// silently dropped values or diverged from the regular mapping.

public class UpdGeo { public double Lat { get; set; } }

public class UpdAddress
{
    public string? Street { get; set; }
    public UpdGeo? Geo { get; set; }
}

public struct UpdPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class UpdBaseTarget
{
    public UpdAddress? Inherited { get; set; }
    public List<string>? BaseTags { get; set; }
}

public class UpdTarget : UpdBaseTarget
{
    public string? Name { get; set; }
    public UpdAddress? Billing { get; set; }
    public UpdAddress? Shipping { get; set; }
    public UpdPoint Position { get; set; }
    public UpdPoint? OptionalPosition { get; set; }
    public string? Display { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdSource
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public double Lat { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class UpdRequiredTarget
{
    public required string Name { get; set; }
    public UpdRequiredChild? Child { get; set; }
}

public class UpdRequiredChild
{
    public required string Value { get; set; }
}

public class UpdInitChild
{
    public string? Value { get; init; }
}

public class UpdCtorChild(string value)
{
    public string Value { get; } = value;
    public int Count { get; set; }
}

public class UpdNestedHolder
{
    public string? Name { get; set; }
    public UpdInitChild? InitChild { get; set; }
    public UpdCtorChild? CtorChild { get; set; }
    public UpdAddress Fixed { get; } = new();
}

[Updatable]
public static partial class UpdatableSemanticsMapper
{
    public static UpdTarget Map(UpdSource source) => new UpdTarget
    {
        Name = source.Name,
        Billing = new UpdAddress { Street = source.Street, Geo = new UpdGeo { Lat = source.Lat } },
        Shipping = new UpdAddress { Street = source.Street, Geo = new UpdGeo { Lat = source.Lat } },
        Inherited = new UpdAddress { Street = source.Street, Geo = new UpdGeo { Lat = source.Lat } },
        Position = new UpdPoint { X = source.X, Y = source.Y },
        OptionalPosition = source.IsActive ? new UpdPoint { X = source.Y, Y = source.X } : null,
        Display = $"{source.Price:0.00}|{source.Name,6}|{{literal}}",
        Tags = source.Tags.ToList(),
        BaseTags = source.Tags.ToList(),
    };

    public static UpdTarget OnlyActive(UpdSource source) => source.IsActive ? new UpdTarget { Name = source.Name } : null!;

    public static UpdTarget OnlyInactive(UpdSource? source) => source == null || source.IsActive ? default! : new UpdTarget { Name = source.Name };

    public static UpdRequiredTarget MapRequired(UpdSource source) => new UpdRequiredTarget
    {
        Name = source.Name ?? "",
        Child = new UpdRequiredChild { Value = source.Street ?? "" }
    };

    public static UpdNestedHolder MapNested(UpdSource source) => new UpdNestedHolder
    {
        Name = source.Name,
        InitChild = new UpdInitChild { Value = source.Street },
        CtorChild = new UpdCtorChild(source.Street ?? "") { Count = source.X },
        Fixed = { Street = source.Street }
    };
}

[Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
public static partial class UpdatableCollectionUpdateMapper
{
    public static UpdTarget Map(UpdSource source) => new UpdTarget
    {
        Name = source.Name,
        Billing = new UpdAddress { Street = source.Street, Geo = new UpdGeo { Lat = source.Lat } },
        Inherited = new UpdAddress { Street = source.Street },
        Tags = source.Tags.ToList(),
        BaseTags = source.Tags.ToList(),
    };
}
