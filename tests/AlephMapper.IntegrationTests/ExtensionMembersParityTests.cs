using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class ExtensionMembersParityTests
{
    [Test]
    public async Task CSharp14_properties_methods_and_classic_extensions_compose()
    {
        foreach (var value in new[] { -1, 0, 3, 100 })
        {
            var order = new ExtensionOrder { Id = value, Amount = value };
            await Assert.That(OrderExtensions.AmountWithTaxExpression().Compile()(order)).IsEqualTo(order.AmountWithTax);
            await Assert.That(OrderExtensions.LabelExpression(3).Compile()(order)).IsEqualTo(order.Label(3));
            await Assert.That(OrderExtensions.ToDtoExpression().Compile()(order)).IsEqualTo(OrderExtensions.ToDto(order));
            await Assert.That(OrderExtensions.ComposedExpression().Compile()(order)).IsEqualTo(order.Composed);
            await Assert.That(OrderExtensions.ClassicExpression().Compile()(order)).IsEqualTo(order.Classic());
            await Assert.That(OrderExtensions.NamedExpression().Compile()(order)).IsEqualTo(OrderExtensions.Named(order));
            await Assert.That(ScalarExtensions.SquaredExpression().Compile()(value)).IsEqualTo(value.Squared);
            await Assert.That(ScalarExtensions.InterfaceLabelExpression().Compile()(order)).IsEqualTo(((IExtensionEntity)order).InterfaceLabel);
        }
    }

    [Test]
    public async Task Extension_members_preserve_nested_receivers_null_policies_and_method_groups()
    {
        var order = new ExtensionOrder { Amount = 2, Other = new() { Amount = 7 } };
        await Assert.That(OrderExtensions.NestedExpression().Compile()(order)).IsEqualTo(OrderExtensions.Nested(order));
        await Assert.That(OrderExtensions.IgnoreExpression().Compile()(order)).IsEqualTo(OrderExtensions.Ignore(order));
        await Assert.That(OrderExtensions.RewriteExpression().Compile()(order)).IsEqualTo(OrderExtensions.Rewrite(order));
        order.Other = null;
        await Assert.That(OrderExtensions.RewriteExpression().Compile()(order)).IsEqualTo(OrderExtensions.Rewrite(order));
        foreach (var sample in new[] { new ExtensionPoint(2, 3), new ExtensionPoint(-1, 4) })
            await Assert.That(ScalarExtensions.SumExpression().Compile()(sample)).IsEqualTo(sample.Sum);
        var input = new[] { new ExtensionOrder { Amount = 3 }, new ExtensionOrder { Amount = 8 } };
        await Assert.That(OrderExtensions.GroupExpression().Compile()(input)).IsEquivalentTo(OrderExtensions.Group(input));
    }

    [Test]
    public async Task CSharp14_projection_executes_in_SQLite()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new ExtensionContext(new DbContextOptionsBuilder<ExtensionContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        context.Orders.AddRange(new ExtensionOrder { Id = 1, Amount = 2 }, new ExtensionOrder { Id = 2, Amount = 120 });
        await context.SaveChangesAsync();
        var actual = await context.Orders.OrderBy(o => o.Id).Select(OrderExtensions.ToDtoExpression()).ToListAsync();
        var expected = (await context.Orders.OrderBy(o => o.Id).ToListAsync()).Select(OrderExtensions.ToDto);
        await Assert.That(actual).IsEquivalentTo(expected);
    }
}

public interface IExtensionEntity { int Id { get; } }
public sealed class ExtensionOrder : IExtensionEntity
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public ExtensionOrder? Other { get; set; }
}
public readonly record struct ExtensionPoint(int X, int Y);
public record ExtensionDto(int Id, decimal AmountWithTax, string Label);
public sealed class ExtensionContext(DbContextOptions<ExtensionContext> options) : DbContext(options)
{
    public DbSet<ExtensionOrder> Orders => Set<ExtensionOrder>();
    protected override void OnModelCreating(ModelBuilder model) => model.Entity<ExtensionOrder>().Ignore(o => o.Other);
}

public static partial class OrderExtensions
{
    extension(ExtensionOrder order)
    {
        [Projectable] public decimal AmountWithTax => order.Amount * 1.25m;
        [Projectable] public string Label(int min) => order.Amount switch { _ when order.Amount >= min => "high", _ => "low" };
        [Projectable] public decimal Composed => order.AmountWithTax + order.Times(2) + order.Classic();
        [Projectable] public decimal Times(int factor) => order.Amount * factor;
        [Projectable] public decimal Subtract(int left, int right) => order.Amount + left - right;
    }
    [Projectable] public static decimal Nested(ExtensionOrder order) => order.Other!.Composed * 2;
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
    public static decimal? Ignore(ExtensionOrder order) => order.Other?.AmountWithTax;
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static decimal? Rewrite(ExtensionOrder order) => order.Other?.Times(2) + order.Other?.AmountWithTax;
    [Projectable] public static decimal[] Group(ExtensionOrder[] orders) => orders.Select(ReadAmount).ToArray();
    private static decimal ReadAmount(ExtensionOrder order) => order.AmountWithTax;
    [Projectable] public static ExtensionDto ToDto(ExtensionOrder order) => new(order.Id, order.AmountWithTax, order.Label(100));
    [Projectable] public static decimal Classic(this ExtensionOrder order) => order.AmountWithTax + 1;
    [Projectable] public static decimal Named(ExtensionOrder order) => order.Subtract(right: 2, left: 7);
}
public static partial class ScalarExtensions
{
    extension(ExtensionPoint point) { [Projectable] public int Sum => point.X + point.Y; }
    extension(int n) { [Projectable] public int Squared => n*n; }
    extension(IExtensionEntity entity) { [Projectable] public string InterfaceLabel => "#" + entity.Id; }
}
