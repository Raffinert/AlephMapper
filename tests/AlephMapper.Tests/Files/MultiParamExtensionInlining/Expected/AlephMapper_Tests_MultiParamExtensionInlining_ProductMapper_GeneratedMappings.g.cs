using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParamExtensionInlining;

[GeneratedCode("AlephMapper", "0.5.5")]
partial class ProductMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(Product)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Product, ProductDto>> ToDtoExpression() => 
        product => new ProductDto
        {
            Label = product.Name,
            PriceTag = "$" + product.Price,
            Total = product.Price * product.Quantity * (1 + 0.1m) - 5m
        };
}
