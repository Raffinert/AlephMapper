using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.Tests;

public class DebugTests
{
    private SqliteConnection _connection = null!;
    private TestDbContext _context = null!;

    [Before(Test)]
    public async Task Setup()
    {
        // Create in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options;

        _context = new TestDbContext(options);

        // Ensure the database is created and seeded
        await _context.Database.EnsureCreatedAsync();
        await SeedAdditionalTestData();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedAdditionalTestData()
    {
        // Add some addresses
        _context.Addresses.AddRange(
            new Address { Id = 1, PersonId = 1, Street = "123 Main St", City = "Kyiv", Country = "Ukraine", IsActive = true },
            new Address { Id = 2, PersonId = 1, Street = "456 Oak Ave", City = "Lviv", Country = "Ukraine", IsActive = false },
            new Address { Id = 3, PersonId = 2, Street = "789 Pine Rd", City = "Lviv", Country = "Ukraine", IsActive = true },
            new Address { Id = 4, PersonId = 3, Street = "321 Elm St", City = "New York", Country = "USA", IsActive = true }
        );

        // Add some orders
        _context.Orders.AddRange(
            new Order { Id = 1, PersonId = 1, OrderNumber = "ORD001", Amount = 150.50m, OrderDate = DateTime.Now.AddDays(-10), IsCompleted = true },
            new Order { Id = 2, PersonId = 1, OrderNumber = "ORD002", Amount = 275.75m, OrderDate = DateTime.Now.AddDays(-5), IsCompleted = true },
            new Order { Id = 3, PersonId = 1, OrderNumber = "ORD003", Amount = 89.99m, OrderDate = DateTime.Now.AddDays(-2), IsCompleted = false },
            new Order { Id = 4, PersonId = 2, OrderNumber = "ORD004", Amount = 450.00m, OrderDate = DateTime.Now.AddDays(-7), IsCompleted = true },
            new Order { Id = 5, PersonId = 3, OrderNumber = "ORD005", Amount = 1250.25m, OrderDate = DateTime.Now.AddDays(-15), IsCompleted = true },
            new Order { Id = 6, PersonId = 3, OrderNumber = "ORD006", Amount = 325.50m, OrderDate = DateTime.Now.AddDays(-3), IsCompleted = true }
        );

        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Debug_Expression_Generation()
    {
        // This test is primarily for debugging and inspection of generated expressions

        // Arrange
        var bornInKyivExpression = Mapper.BornInKyivExpression();
        var bornInKyivAndOlder35Expression = Mapper.BornInKyivAndOlder35Expression();

        // Act & Assert - Output expressions for debugging
        Console.WriteLine("BornInKyiv Expression:");
        Console.WriteLine(bornInKyivExpression.ToString());
        Console.WriteLine("");

        Console.WriteLine("LivesInKyivAndOlder35 Expression:");
        Console.WriteLine(bornInKyivAndOlder35Expression.ToString());
        Console.WriteLine("");

        // Verify they compile and work
        var bornInKyivCompiled = bornInKyivExpression.Compile();
        var bornInKyivAndOlder35Compiled = bornInKyivAndOlder35Expression.Compile();

        var testBirthInfo = new BirthInfo { Age = 40, Address = "Kyiv" };
        var testSourceDto = new SourceDto { BirthInfo = testBirthInfo };

        await Assert.That(bornInKyivCompiled(testBirthInfo)).IsTrue();
        await Assert.That(bornInKyivAndOlder35Compiled(testSourceDto)).IsTrue();
    }

    [Test]
    public async Task Debug_EfCore_Query_Generation()
    {
        // This test helps debug EF Core SQL generation

        // Arrange
        var personSummaryExpression = EfCoreMapper.GetPersonSummaryExpression();

        // Act
        var query = _context.Persons.Select(personSummaryExpression);

        Console.WriteLine("Generated SQL:");
        Console.WriteLine(query.ToQueryString());
        Console.WriteLine("");

        var results = await query.ToListAsync();

        // Assert
        await Assert.That(results.Count).IsEqualTo(4);

        foreach (var result in results)
        {
            Console.WriteLine($"Result: {result}");
        }
    }

    [Test]
    public async Task Debug_Complex_Expression_With_Null_Conditional()
    {
        // Debug expressions that use null conditional operators

        // Arrange
        var rewriteExpression = RewriteMapper.GetAddressExpression();
        var ignoreExpression = IgnoreMapper.GetAddressExpression();

        // Act & Assert
        Console.WriteLine("Rewrite Policy Expression:");
        Console.WriteLine(rewriteExpression.ToString());
        Console.WriteLine("");

        Console.WriteLine("Ignore Policy Expression:");
        Console.WriteLine(ignoreExpression.ToString());
        Console.WriteLine("");

        var rewriteCompiled = rewriteExpression.Compile();
        var ignoreCompiled = ignoreExpression.Compile();

        // Test with null values
        var sourceWithNull = new SourceDto { Name = "Test", BirthInfo = null };

        await Assert.That(rewriteCompiled(sourceWithNull)).IsEqualTo("Unknown");

        // With ignore policy, this would throw NullReferenceException
        await Assert.That(() => ignoreCompiled(sourceWithNull)).Throws<NullReferenceException>();
    }
}