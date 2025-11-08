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

public class NestedConditionalExtensionInliningTests
{
    [Test]
    public async Task IgnorePolicy_NestedConditional_Extension_Should_Inline_And_Preserve_Chain()
    {
        var expr = NestedExt_PersonMapper_Ignore.ToDtoExpression();
        var readable = expr.ToReadableString();

        await Assert.That(readable).Contains("new NestedExt_AddressDto");
        await Assert.That(readable).DoesNotContain("?.ToDto()");
        await Assert.That(readable).Contains("Friend");
        await Assert.That(readable).Contains("HomeAddress");
    }

    [Test]
    public async Task RewritePolicy_NestedConditional_Extension_Should_Add_Null_Checks()
    {
        var expr = NestedExt_PersonMapper_Rewrite.ToDtoExpression();
        var exprStr = expr.ToString();

        await Assert.That(exprStr).Contains("p.Friend != null");
        await Assert.That(exprStr).Contains("p.Friend.HomeAddress != null");
    }

    [Test]
    public async Task Execution_Works_For_Null_And_NonNull()
    {
        var withAll = new NestedExt_Person
        {
            Friend = new NestedExt_Person
            {
                HomeAddress = new NestedExt_Address { Street = "S", City = "C" }
            }
        };

        var withoutFriend = new NestedExt_Person { Friend = null };
        var withoutHome = new NestedExt_Person { Friend = new NestedExt_Person { HomeAddress = null } };

        var dto1 = NestedExt_PersonMapper_Ignore.ToDto(withAll);
        await Assert.That(dto1.HomeAddress).IsNotNull();
        await Assert.That(dto1.HomeAddress!.Street).IsEqualTo("S");

        var dto2 = NestedExt_PersonMapper_Ignore.ToDto(withoutFriend);
        await Assert.That(dto2.HomeAddress).IsNull();

        var dto3 = NestedExt_PersonMapper_Ignore.ToDto(withoutHome);
        await Assert.That(dto3.HomeAddress).IsNull();
    }
}
