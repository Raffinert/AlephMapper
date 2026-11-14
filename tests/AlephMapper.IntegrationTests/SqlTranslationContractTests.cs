using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class SqlTranslationContractTests
{
    [Test]
    public async Task Generated_scalar_computation_is_in_SQL_not_only_client_projection()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(connection).Options;
        await using var context = new ComprehensiveTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var generated = context.Employees.Select(SqlContractMapper.FullNameExpression());
        var control = context.Employees.Select(employee => SqlContractMapper.FullName(employee));
        var generatedSql = generated.ToQueryString();
        var controlSql = control.ToQueryString();

        await SqlSnapshot.AssertMatches("scalar-generated", generatedSql);
        await SqlSnapshot.AssertMatches("scalar-control", controlSql);
        await Assert.That(await generated.OrderBy(name => name).FirstAsync()).IsEqualTo("Alice Brown");
    }

    [Test]
    public async Task Generated_predicate_filters_in_SQL()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(connection).Options;
        await using var context = new ComprehensiveTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var query = context.Employees.Where(SqlContractMapper.ActiveJohnExpression());
        var sql = query.ToQueryString();

        await SqlSnapshot.AssertMatches("predicate", sql);
        await Assert.That((await query.ToListAsync()).Select(employee => employee.Id).Single()).IsEqualTo(1);
    }

    [Test]
    public async Task Rewritten_null_navigation_is_in_SQL()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(connection).Options;
        await using var context = new ComprehensiveTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var query = context.Employees.OrderBy(employee => employee.Id)
            .Select(SqlContractMapper.DepartmentNameExpression());
        var sql = query.ToQueryString();
        var values = await query.ToListAsync();

        await SqlSnapshot.AssertMatches("navigation", sql);
        await Assert.That(values[0]).IsEqualTo("Engineering");
        await Assert.That(values[3]).IsEqualTo("Unassigned");
    }
}
