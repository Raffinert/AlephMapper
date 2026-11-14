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
            await Assert.That(OrderExtensions.TimesExpression(2L).Compile()(order)).IsEqualTo(order.Times(2L));
            await Assert.That(ScalarExtensions.SquaredExpression().Compile()(value)).IsEqualTo(value.Squared);
            await Assert.That(ScalarExtensions.InterfaceLabelExpression().Compile()(order)).IsEqualTo(order.InterfaceLabel);
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
        foreach (var sample in new int?[] { null, -1, 3 })
            await Assert.That(ScalarExtensions.OrZeroExpression().Compile()(sample)).IsEqualTo(sample.OrZero);
        foreach (var sample in new[] { new ExtensionPoint(2, 3), new ExtensionPoint(-1, 4) })
            await Assert.That(ScalarExtensions.SumExpression().Compile()(sample)).IsEqualTo(sample.Sum);
        var input = new[] { new ExtensionOrder { Amount = 3 }, new ExtensionOrder { Amount = 8 } };
        await Assert.That(OrderExtensions.GroupExpression().Compile()(input)).IsEquivalentTo(OrderExtensions.Group(input));
    }

    [Test]
    public async Task Receiver_substitutions_do_not_capture_lambda_names_or_lose_static_binding()
    {
        await Assert.That(ExtensionReviewMapper.CaptureExpression().Compile()(10)).IsEqualTo(ExtensionReviewMapper.Capture(10));
        await Assert.That(ExtensionReviewMapper.Capture(10)).IsEqualTo(14);
        await Assert.That(ExtensionReviewMapper.NameExpression().Compile()(10)).IsEqualTo(ExtensionReviewMapper.Name(10));
        await Assert.That(ExtensionReviewMapper.Name(10)).IsEqualTo("receiver");
        await Assert.That(ExtensionReviewMapper.StaticDependencyExpression().Compile()(10)).IsEqualTo(ExtensionReviewMapper.StaticDependency(10));
        await Assert.That(ExtensionReviewMapper.NonfiniteExpression().Compile()(10)).IsEqualTo(ExtensionReviewMapper.Nonfinite(10));
        await Assert.That(ExtensionReviewMapper.Nonfinite(10)).IsTrue();
        var derived = new ExtensionDerived { Value = 99 };
        ((ExtensionBase)derived).Value = 3;
        await Assert.That(ExtensionReviewMapper.ConditionalExpression().Compile()(derived)).IsEqualTo(3);
        await Assert.That(ExtensionReviewMapper.ConditionalExpression().Compile()(null)).IsNull();
        await Assert.That(ExtensionReviewMapper.ConditionalIgnoreExpression().Compile()(derived)).IsEqualTo(3);
    }

    [Test]
    public async Task Classic_lambda_renaming_preserves_nameof_and_generic_static_binding()
    {
        var values = new[] { 1, 2, 3 };
        await Assert.That(ExtensionReviewMapper.ClassicNameExpression().Compile()(values)).IsEqualTo(ExtensionReviewMapper.ClassicName(values));
        await Assert.That(ExtensionReviewMapper.ClassicName(values)).IsEqualTo(9);
        await Assert.That(ExtensionReviewMapper.GenericDependencyExpression().Compile()(10)).IsEqualTo(ExtensionReviewMapper.GenericDependency(10));
        await Assert.That(ExtensionReviewMapper.GenericDependency(10)).IsEqualTo(13);
    }

    [Test]
    public async Task Renamed_lambda_preserves_anonymous_members_and_root_nameof()
    {
        var values = new[] { 1, 2, 3 };
        await Assert.That(ExtensionReviewMapper.AnonymousNameExpression().Compile()(values)).IsEqualTo(ExtensionReviewMapper.AnonymousName(values));
        await Assert.That(ExtensionReviewMapper.AnonymousName(values)).IsEqualTo(4);
        var order = new ExtensionOrder();
        await Assert.That(ExtensionReviewMapper.RootNameExpression().Compile()(order)).IsEqualTo(ExtensionReviewMapper.RootName(order));
        await Assert.That(ExtensionReviewMapper.RootName(order)).IsEqualTo("Amount");
    }

    [Test]
    public async Task Shared_expansion_preserves_classic_bindings_and_method_group_return_conversion()
    {
        await Assert.That(ExtensionReviewMapper.MixedNamedExpression().Compile()(10)).IsEqualTo(8);
        await Assert.That(ExtensionReviewMapper.MixedNamedExpression().ToString()).DoesNotContain("Subtract");
        await Assert.That(ExtensionReviewMapper.MixedClassicExpression().Compile()(10)).IsEqualTo(15);
        await Assert.That(ExtensionReviewMapper.MixedClassicExpression().ToString()).DoesNotContain("Plus");
        var input = new[] { 16777217 };
        await Assert.That(ExtensionReviewMapper.RoundedGroupExpression().Compile()(input))
            .IsEquivalentTo(ExtensionReviewMapper.RoundedGroup(input));
    }

    [Test]
    public async Task CSharp14_extension_update_and_adapt_companions_preserve_destinations()
    {
        var order = new ExtensionOrder { Id = 7, Amount = 12m };
        var destination = new ExtensionObjectDto { Id = -1, Amount = -1m };
        var updated = ExtensionMappingCompanions.ToObjectDto(order, 3, destination);

        await Assert.That(ReferenceEquals(updated, destination)).IsTrue();
        await Assert.That(destination.Id).IsEqualTo(10);
        await Assert.That(destination.Amount).IsEqualTo(12m);
        await Assert.That(ExtensionMappingCompanions.ToObjectDtoExpression(3).Compile()(order).Id).IsEqualTo(10);

        var alternative = new AlternativeOrder { Id = 11, Amount = 8m };
        var mapped = ExtensionMappingCompanions.ToAlternativeDto(alternative, 2);
        await Assert.That(mapped.Id).IsEqualTo(13);
        await Assert.That(mapped.Amount).IsEqualTo(8m);
        await Assert.That(ExtensionMappingCompanions.ToAlternativeDtoExpression(2).Compile()(alternative).Id).IsEqualTo(13);

        var alternativeDestination = new AlternativeOrderDto { Id = -1, Amount = -1m };
        var alternativeUpdated = ExtensionMappingCompanions.ToAlternativeDto(alternative, 4, alternativeDestination);
        await Assert.That(ReferenceEquals(alternativeUpdated, alternativeDestination)).IsTrue();
        await Assert.That(alternativeDestination.Id).IsEqualTo(15);
        await Assert.That(alternativeDestination.Amount).IsEqualTo(8m);
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

        var objectDtos = await context.Orders.OrderBy(o => o.Id)
            .Select(ExtensionMappingCompanions.ToObjectDtoExpression(5)).ToListAsync();
        await Assert.That(objectDtos.Select(dto => dto.Id)).IsEquivalentTo(new[] { 6, 7 });
    }
}

public sealed class ExtensionObjectDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public sealed class AlternativeOrder
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public sealed class AlternativeOrderDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public static partial class ExtensionMappingCompanions
{
    extension(ExtensionOrder order)
    {
        [Projectable]
        [Updatable]
        [Adapt(typeof(AlternativeOrder), typeof(AlternativeOrderDto), Name = "ToAlternativeDto",
            Generate = AdaptGeneration.Map | AdaptGeneration.Expression | AdaptGeneration.Update)]
        public ExtensionObjectDto ToObjectDto(int offset) => new()
        {
            Id = order.Id + offset,
            Amount = order.Amount
        };
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
        [Projectable] public decimal Times(long factor) => order.Amount * factor;
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
    [Projectable] public static decimal Named(ExtensionOrder order) => order.Subtract(right: 2, left: 7) + order.Subtract(left: 7, 2);
}
public static partial class ScalarExtensions
{
    extension(int? n) { [Projectable] public int OrZero => n ?? 0; }
    extension(ExtensionPoint point) { [Projectable] public int Sum => point.X + point.Y; }
    extension(int n) { [Projectable] public int Squared => n*n; }
    extension(IExtensionEntity entity) { [Projectable] public string InterfaceLabel => "#" + entity.Id; }
}

public class ExtensionBase { public int Value { get; set; } }
public class ExtensionDerived : ExtensionBase { public new int Value { get; set; } }
public static partial class ReviewExtensions
{
    private const int Offset = 3;
    private const double NotANumber = double.NaN;
    private const float Infinite = float.PositiveInfinity;
    public static int PublicOffset = 4;
    public static int GenericOffset<T>() { return 3; }
    extension(int receiver)
    {
        [Projectable] public int WithGenericOffset => receiver + GenericOffset<int>();
        [Projectable] public string ReceiverName => nameof(receiver);
        [Projectable] public int Sum(int add) => new[] { 1 }.Sum(x => x + receiver + add);
        [Projectable] public bool WithNonfinite => double.IsNaN(receiver + NotANumber) && float.IsPositiveInfinity(receiver + Infinite);
        [Projectable] public int WithOffset => receiver + Offset + PublicOffset;
    }
    extension(ExtensionBase receiver) { [Projectable] public int Score => receiver.Value; }
}
public static partial class ExtensionReviewMapper
{
    public static int GenericOffset<T>() { return 99; }
    [Projectable] public static int GenericDependency(int n) => n.WithGenericOffset;
    [Projectable] public static int ClassicName(int[] values) => ClassicHelper(values.Length);
    private static int ClassicHelper(int n) => new[] { 1 }.Sum(Length => n + nameof(Length).Length);
    [Projectable] public static int MixedNamed(int value) => Subtract(left: value, 2);
    private static int Subtract(int left, int right) => left-right;
    [Projectable] public static int MixedClassic(int value) => value.Plus(left: 7, 2);
    private static int Plus(this int value, int left, int right) => value+left-right;
    [Projectable] public static double[] RoundedGroup(int[] values) => values.Select(Narrow).Select(value => (double)value).ToArray();
    private static float Narrow(int value) => value;
    [Projectable] public static string RootName(ExtensionOrder order) => nameof(order.Amount);
    [Projectable] public static int AnonymousName(int[] values) => AnonymousHelper(values.Length);
    private static int AnonymousHelper(int n) => new[] { 1 }.Select(Length => new { Length, Value = n }).Sum(v => v.Length + v.Value);
    [Projectable] public static string Name(int n) => n.ReceiverName;
    [Projectable] public static bool Nonfinite(int x) => x.WithNonfinite;
    [Projectable] public static int Capture(int x) => x.Sum(3);
    [Projectable] public static int StaticDependency(int x) => x.WithOffset;
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static int? Conditional(ExtensionDerived? value) => value?.Score;
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
    public static int? ConditionalIgnore(ExtensionDerived? value) => value?.Score;
}
