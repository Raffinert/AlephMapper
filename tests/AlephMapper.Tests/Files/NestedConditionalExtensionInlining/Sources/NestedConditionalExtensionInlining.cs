using AgileObjects.ReadableExpressions;

namespace AlephMapper.Tests;

public class NestedExt_Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class NestedExt_AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class NestedExt_Person
{
    public NestedExt_Address? HomeAddress { get; set; }
    public NestedExt_Person? Friend { get; set; }
}

public class NestedExt_PersonDto
{
    public NestedExt_AddressDto? HomeAddress { get; set; }
}

public static partial class NestedExt_AddressMapper
{
    [Expressive]
    public static NestedExt_AddressDto ToDto(this NestedExt_Address a) => new()
    {
        Street = a.Street,
        City = a.City
    };
}

// Mapper using nested conditional access chains

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class NestedExt_PersonMapper_Ignore
{
    [Expressive]
    public static NestedExt_PersonDto ToDto(NestedExt_Person p) => new()
    {
        HomeAddress = p?.Friend?.HomeAddress?.ToDto()
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class NestedExt_PersonMapper_Rewrite
{
    [Expressive]
    public static NestedExt_PersonDto ToDto(NestedExt_Person p) => new()
    {
        HomeAddress = p?.Friend?.HomeAddress?.ToDto()
    };
}