using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection)
    {
        _connection = connection;
        Context = CreateContext();
    }

    public ComprehensiveTestDbContext Context { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        SqliteTestDatabase? database = null;
        try
        {
            await connection.OpenAsync();
            database = new SqliteTestDatabase(connection);
            await database.Context.Database.EnsureCreatedAsync();
            return database;
        }
        catch
        {
            if (database is null) await connection.DisposeAsync();
            else await database.DisposeAsync();
            throw;
        }
    }

    public ComprehensiveTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ComprehensiveTestDbContext>().UseSqlite(_connection).Options);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Context.DisposeAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }
}
