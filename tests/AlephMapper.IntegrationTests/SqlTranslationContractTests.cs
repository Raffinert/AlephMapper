using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class SqlTranslationContractTests
{
    private SqliteConnection _connection = null!;
    private ComprehensiveTestDbContext _context = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(_connection).Options;
        _context = new ComprehensiveTestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task Generated_scalar_computation_is_in_SQL_not_only_client_projection()
    {
        var generated = _context.Employees.Select(SqlContractMapper.FullNameExpression());
        var control = _context.Employees.Select(employee => SqlContractMapper.FullName(employee));

        await SqlSnapshot.AssertMatches("scalar-generated", generated.ToQueryString());
        await SqlSnapshot.AssertMatches("scalar-control", control.ToQueryString());
        await Assert.That(await generated.OrderBy(name => name).FirstAsync()).IsEqualTo("Alice Brown");
    }

    [Test]
    public async Task Generated_predicate_filters_in_SQL()
    {
        var query = _context.Employees.Where(SqlContractMapper.ActiveJohnExpression());

        await SqlSnapshot.AssertMatches("predicate", query.ToQueryString());
        await Assert.That((await query.ToListAsync()).Select(employee => employee.Id).Single()).IsEqualTo(1);
    }

    [Test]
    public async Task Rewritten_null_navigation_is_in_SQL()
    {
        var query = _context.Employees.OrderBy(employee => employee.Id)
            .Select(SqlContractMapper.DepartmentNameExpression());

        await SqlSnapshot.AssertMatches("navigation", query.ToQueryString());
        var values = await query.ToListAsync();
        await Assert.That(values[0]).IsEqualTo("Engineering");
        await Assert.That(values[3]).IsEqualTo("Unassigned");
    }
}
