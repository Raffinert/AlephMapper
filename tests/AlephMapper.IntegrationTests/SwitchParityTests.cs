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
public class SwitchDog : SwitchNode { }

public static partial class SwitchParityMapper
{
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
