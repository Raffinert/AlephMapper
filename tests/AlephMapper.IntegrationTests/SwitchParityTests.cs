using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class SwitchParityTests
{
    [Test]
    public async Task Property_patterns_match_original_including_nested_bindings_and_nulls()
    {
        SwitchNode?[] samples = [null, new(), new() { Score = 4 }, new() { Score = -1 },
            new() { Child = new() { Score = 7, Name = "Ada" } },
            new SwitchDog { Score = 8, Name = "Ada" }, new() { Child = new() }];
        foreach (var method in typeof(SwitchParityMapper).GetMethods().Where(m => m.ReturnType == typeof(int) &&
                     m.GetParameters() is [{ ParameterType: var t }] && t == typeof(SwitchNode)))
        {
            var expression = (Expression<Func<SwitchNode?, int>>)typeof(SwitchParityMapper)
                .GetMethod(method.Name + "Expression")!.Invoke(null, null)!;
            var compiled = expression.Compile();
            foreach (var sample in samples)
                await Assert.That(compiled(sample)).IsEqualTo((int)method.Invoke(null, [sample])!);
        }
    }

    [Test]
    public async Task List_patterns_match_original_at_length_boundaries()
    {
        int[]?[] samples = [null, [], [0], [1], [1, 3], [1, 2, 3], [1, 2, 3, 4], [-1, 3]];
        foreach (var sample in samples)
        {
            await Assert.That(SwitchParityMapper.ArrayExpression().Compile()(sample)).IsEqualTo(SwitchParityMapper.Array(sample));
            await Assert.That(SwitchParityMapper.SliceExpression().Compile()(sample)).IsEqualTo(SwitchParityMapper.Slice(sample));
            await Assert.That(SwitchParityMapper.SuffixExpression().Compile()(sample)).IsEqualTo(SwitchParityMapper.Suffix(sample));
            await Assert.That(SwitchParityMapper.ListExpression().Compile()(sample?.ToList())).IsEqualTo(SwitchParityMapper.List(sample?.ToList()));
        }
    }

    [Test]
    public async Task Record_patterns_match_original()
    {
        foreach (var sample in new SwitchPoint?[] { null, new(0, 0), new(2, 3), new(-1, 3), new(3, -1) })
            await Assert.That(SwitchParityMapper.PointExpression().Compile()(sample)).IsEqualTo(SwitchParityMapper.Point(sample));
        foreach (var sample in new[] { new SwitchValuePoint(0, 0), new SwitchValuePoint(2, 3), new SwitchValuePoint(-1, 2) })
            await Assert.That(SwitchParityMapper.ValuePointExpression().Compile()(sample)).IsEqualTo(SwitchParityMapper.ValuePoint(sample));
    }

    [Test]
    public async Task Nullable_constants_results_and_nested_scopes_match_original()
    {
        foreach (var x in new int?[] { null, -1, 0, 1, 4 })
        {
            await Assert.That(SwitchParityMapper.NullableIntExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.NullableInt(x));
            await Assert.That(SwitchParityMapper.ResultExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.Result(x));
        }
        foreach (var x in new decimal?[] { null, -1m, 0m, 1m, 4m })
            await Assert.That(SwitchParityMapper.DecimalExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.Decimal(x));
        foreach (var x in new char?[] { null, 'a', 'b' })
            await Assert.That(SwitchParityMapper.CharExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.Char(x));
        foreach (var x in new DayOfWeek?[] { null, DayOfWeek.Monday, DayOfWeek.Friday })
            await Assert.That(SwitchParityMapper.EnumExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.Enum(x));
        foreach (var x in new SwitchValuePoint?[] { null, new(1, 2), new(-1, 2) })
            await Assert.That(SwitchParityMapper.NullablePointExpression().Compile()(x)).IsEqualTo(SwitchParityMapper.NullablePoint(x));
    }

    [Test]
    public async Task Property_pattern_executes_in_SQLite()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new ComprehensiveTestDbContext(new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var employees = await context.Employees.OrderBy(e => e.Id).ToListAsync();
        var actual = await context.Employees.OrderBy(e => e.Id).Select(SwitchParityMapper.EmployeeGradeExpression()).ToListAsync();
        await Assert.That(actual).IsEquivalentTo(employees.Select(SwitchParityMapper.EmployeeGrade));
    }
}

public class SwitchNode
{
    public SwitchNode? Child { get; set; }
    public int Score { get; set; }
    public string? Name { get; set; }
}
public record SwitchPoint(int X, int Y);
public readonly record struct SwitchValuePoint(int X, int Y);
public class SwitchDog : SwitchNode { }

public static partial class SwitchParityMapper
{
    [Projectable] public static int Point(SwitchPoint? value) => value switch
        { SwitchPoint(0, 0) => 0, SwitchPoint(var x, > 0) when x > 0 => x, _ => -1 };
    [Projectable] public static int ValuePoint(SwitchValuePoint value) => value switch
        { (0, 0) => 0, (var x, > 0) when x > 0 => x, _ => -1 };
    [Projectable] public static int NullableInt(int? x) => x switch { null => -1, 1 => 1, > 1 and < 5 => 2, _ => 0 };
    [Projectable] public static object? Result(int? x) => x switch { null => null, 1 => 1, _ => 2L };
    [Projectable] public static int Decimal(decimal? x) => x switch { null => -1, 1m => 1, >= 2m => 2, _ => 0 };
    [Projectable] public static int Char(char? x) => x switch { null => -1, 'a' => 1, _ => 0 };
    [Projectable] public static int Enum(DayOfWeek? x) => x switch { null => -1, DayOfWeek.Monday => 1, _ => 0 };
    [Projectable] public static int NullablePoint(SwitchValuePoint? value) => value switch { { X: > 0 } => 1, _ => 0 };
    [Projectable] public static int IsGuard(SwitchNode? value) => value switch
        { {} when value is { Name: not null } => 1, _ => 0 };
    [Projectable] public static int NestedSwitch(SwitchNode? value) => value switch
        { { Child: SwitchNode c } => c switch { { Score: > 0 } d => d.Score + c.Score, _ => -1 }, _ => -2 };
    [Projectable] public static int NestedElements(SwitchNode? value) => new[] { value } switch
        { [{ Child: SwitchNode c }] when c.Score > 0 => c.Score, _ => -1 };
    [Projectable] public static int Array(int[]? value) => value switch
        { [] => 0, [var x] when x > 0 => x, [1, 3] => 13, _ => -1 };
    [Projectable] public static int Slice(int[]? value) => value switch
        { [1, .., 3] => 13, [1, ..] => 1, [..] => 0, _ => -1 };
    [Projectable] public static int Suffix(int[]? value) => value switch
        { [.., var last] when last > 0 => last, _ => -1 };
    [Projectable] public static int List(List<int>? value) => value switch
        { [] => 0, [var first, .., var last] when first > 0 => first + last, _ => -1 };
    [Projectable] public static int Var(SwitchNode? value) => value switch { var x when x == null => 1, _ => 0 };
    [Projectable] public static int Type(SwitchNode? value) => value switch { SwitchDog => 2, _ => 0 };
    [Projectable] public static int Empty(SwitchNode? value) => value switch { {} => 1, _ => 0 };
    [Projectable] public static int Nested(SwitchNode? value) => value switch
        { { Child: SwitchNode c } when c.Score > 0 => c.Score, _ => -1 };
    [Projectable] public static int Extended(SwitchNode? value) => value switch
        { { Child.Score: > 0 } => 1, _ => -1 };
    [Projectable] public static int Typed(SwitchNode? value) => value switch
        { SwitchDog { Score: > 0, Name: not null } dog => dog.Score, _ => -1 };
    [Projectable] public static int Logical(SwitchNode? value) => value switch
        { not { Score: > 0 } => 1, { Score: > 0 } and { Name: not null } => 2, _ => 0 };
    [Projectable] public static int Helper(SwitchNode? value) => Nested(value);
    [Projectable] public static int EmployeeGrade(Employee value) => value switch
        { { Id: >= 4 and <= 5 } => 2, { Id: > 0 } => 1, _ => 0 };
}
