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

// Shapes found in review: keyword names, getter-only overrides, derived creations,
// generic members, conditional typing and throw guards.

public class UpdKeyword { public string? @event { get; set; } }

public class UpdVirtualBase { public virtual string? Name { get; set; } }

public class UpdGetterOverride : UpdVirtualBase
{
    public override string? Name { get => base.Name; }
}

public class UpdAnimal { public string? Name { get; set; } }

public class UpdDog : UpdAnimal { public bool Barks { get; set; } }

public class UpdZoo
{
    public UpdAnimal? Pet { get; set; }
    public int? Age { get; set; }
    public UpdBag? Bag { get; set; }
}

public class UpdBag { public List<string> Items { get; } = []; }

public interface IUpdNamed { string? Name { get; set; } }

public class UpdNamedClass : IUpdNamed { public string? Name { get; set; } }

public struct UpdNamedStruct : IUpdNamed { public string? Name { get; set; } }

public class UpdWrapper<T> { public T? Value { get; set; } }

[Updatable]
public static partial class UpdatableReviewMapper
{
    public static UpdKeyword MapKeyword(UpdSource source) => new UpdKeyword { @event = source.Name };

    public static UpdGetterOverride MapOverride(UpdSource source) => new UpdGetterOverride { Name = source.Name };

    public static UpdZoo MapZoo(UpdSource source) => new UpdZoo
    {
        Pet = new UpdDog { Name = source.Name, Barks = true },
        Age = source.IsActive ? 5 : default,
    };

    public static UpdAnimal MapAnimal(UpdSource source) => new UpdDog { Name = source.Name };

    public static UpdWrapper<T> MapGeneric<T>(UpdSource source) where T : IUpdNamed, new() =>
        new UpdWrapper<T> { Value = new T { Name = source.Name } };

    public static UpdAnimal MapRequiredSource(UpdSource? source) =>
        source == null ? throw new ArgumentNullException(nameof(source)) : new UpdAnimal { Name = source.Name };
}

[Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
public static partial class UpdatableReviewCollectionMapper
{
    public static UpdZoo MapBag(UpdSource source) => new UpdZoo { Bag = new UpdBag { Items = { source.Name ?? "" } } };
}
