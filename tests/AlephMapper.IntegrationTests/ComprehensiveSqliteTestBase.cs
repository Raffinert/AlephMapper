namespace AlephMapper.IntegrationTests;

public abstract class ComprehensiveSqliteTestBase
{
    private SqliteTestDatabase _database = null!;

    protected ComprehensiveTestDbContext _context => _database.Context;

    [Before(Test)]
    public async Task SetupDatabase()
    {
        _database = await SqliteTestDatabase.CreateAsync();
        try
        {
            await SeedAdditionalDataAsync();
        }
        catch
        {
            await _database.DisposeAsync();
            throw;
        }
    }

    [After(Test)]
    public async Task CleanupDatabase() => await _database.DisposeAsync();

    protected virtual Task SeedAdditionalDataAsync() => Task.CompletedTask;
}
