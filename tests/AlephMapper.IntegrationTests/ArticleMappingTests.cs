using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class ArticleMappingTests
{
    [Test]
    public async Task Mapping_expression_should_inline_helpers_and_project_with_ef_core()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;

        context.ArticleCustomers.AddRange(
            new ArticleCustomer { Id = 1, FirstName = "Ada", LastName = "Lovelace" },
            new ArticleCustomer { Id = 2, FirstName = "Grace", LastName = "Hopper" });
        context.ArticleOrders.AddRange(
            new ArticleOrder
            {
                Id = 10,
                Number = "ORD-10",
                CustomerId = 1,
                Lines =
                [
                    new ArticleOrderLine { Id = 100, ProductName = "Keyboard", Price = 75m, Quantity = 2 },
                    new ArticleOrderLine { Id = 101, ProductName = "Mouse", Price = 25m, Quantity = 1 }
                ]
            },
            new ArticleOrder
            {
                Id = 11,
                Number = "ORD-11",
                CustomerId = 2,
                Lines =
                [new ArticleOrderLine { Id = 102, ProductName = "Monitor", Price = 200m, Quantity = 1 }]
            });
        await context.SaveChangesAsync();

        var projection = context.ArticleOrders
            .OrderBy(order => order.Id)
            .Select(ArticleOrderMapper.MapExpression());
        var sql = projection.ToQueryString();
        await SqlSnapshot.AssertMatches("article", sql);

        var projected = await projection.ToListAsync();

        await Assert.That(projected.Count).IsEqualTo(2);
        await Assert.That(projected[0].Id).IsEqualTo(10);
        await Assert.That(projected[0].Number).IsEqualTo("ORD-10");
        await Assert.That(projected[0].CustomerName).IsEqualTo("Ada Lovelace");
        await Assert.That(projected[0].Total).IsEqualTo(175m);
        await Assert.That(projected[1].CustomerName).IsEqualTo("Grace Hopper");
        await Assert.That(projected[1].Total).IsEqualTo(200m);

        var materialized = await context.ArticleOrders
            .Include(order => order.Customer)
            .Include(order => order.Lines)
            .SingleAsync(order => order.Id == 10);

        var inMemory = ArticleOrderMapper.Map(materialized);
        await Assert.That(inMemory.CustomerName).IsEqualTo(projected[0].CustomerName);
        await Assert.That(inMemory.Total).IsEqualTo(projected[0].Total);
    }
}
