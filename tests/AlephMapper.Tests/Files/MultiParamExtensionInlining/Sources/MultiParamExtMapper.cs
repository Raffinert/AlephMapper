using AlephMapper;

namespace AlephMapper.Tests.MultiParamExtensionInlining;

public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class ProductDto
{
    public string Label { get; set; } = string.Empty;
    public string PriceTag { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

/// <summary>
/// Extension methods with multiple parameters beyond the 'this' parameter.
/// These test the intersection of the extension method bug fix and multi-param inlining.
/// </summary>
public static class ProductExtensions
{
    /// <summary>
    /// 2-param extension: this + one extra argument.
    /// Called as product.FormatPrice("$") => this=product, prefix="$"
    /// </summary>
    public static string FormatPrice(this Product product, string prefix) =>
        prefix + product.Price;

    /// <summary>
    /// 3-param extension: this + two extra arguments.
    /// Called as product.ComputeTotal(taxRate, discount)
    /// </summary>
    public static decimal ComputeTotal(this Product product, decimal taxRate, decimal discount) =>
        product.Price * product.Quantity * (1 + taxRate) - discount;
}

/// <summary>
/// Tests basic multi-param extension method inlining via dot syntax.
/// product.FormatPrice("$") should inline to "$" + product.Price
/// product.ComputeTotal(0.1m, 5m) should inline to product.Price * product.Quantity * (1 + 0.1m) - 5m
/// </summary>
public static partial class ProductMapper
{
    [Expressive]
    public static ProductDto ToDto(Product product) => new()
    {
        Label = product.Name,
        PriceTag = product.FormatPrice("$"),
        Total = product.ComputeTotal(0.1m, 5m)
    };
}

/// <summary>
/// Tests multi-param extension method on a nullable sub-object with conditional access.
/// person.FavoriteProduct?.FormatPrice("$") should be handled correctly.
/// </summary>
public class Person
{
    public string Name { get; set; } = string.Empty;
    public Product? FavoriteProduct { get; set; }
}

public class PersonProductDto
{
    public string Name { get; set; } = string.Empty;
    public string FavoritePrice { get; set; } = string.Empty;
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class PersonProductMapperIgnore
{
    [Expressive]
    public static PersonProductDto ToDto(Person person) => new()
    {
        Name = person.Name,
        FavoritePrice = person.FavoriteProduct?.FormatPrice("$")
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class PersonProductMapperRewrite
{
    [Expressive]
    public static PersonProductDto ToDto(Person person) => new()
    {
        Name = person.Name,
        FavoritePrice = person.FavoriteProduct?.FormatPrice("$")
    };
}
