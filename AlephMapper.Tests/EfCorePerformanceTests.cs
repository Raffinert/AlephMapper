using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AlephMapper.Tests;

public class EfCorePerformanceTests
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
            .EnableSensitiveDataLogging() // For debugging SQL queries
            .Options;

        _context = new TestDbContext(options);

        // Ensure the database is created and seeded with larger dataset
        await _context.Database.EnsureCreatedAsync();
        await SeedLargeDataset();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedLargeDataset()
    {
        var birthInfos = new List<PersonBirthInfo>();
        var persons = new List<Person>();
        var addresses = new List<Address>();
        var orders = new List<Order>();

        // Create 100 birth infos
        for (int i = 1; i <= 100; i++)
        {
            birthInfos.Add(new PersonBirthInfo
            {
                Id = i + 10, // Offset to avoid conflicts with seeded data
                Age = 18 + (i % 50), // Ages between 18-67
                BirthPlace = i % 3 == 0 ? "Kyiv" : i % 3 == 1 ? "Lviv" : "New York",
                Address = i % 3 == 0 ? "Kyiv, Ukraine" : i % 3 == 1 ? "Lviv, Ukraine" : "New York, USA",
                BirthDate = DateTime.Now.AddYears(-(18 + (i % 50)))
            });
        }

        // Create 200 persons (100 with birth info, 100 without)
        for (int i = 1; i <= 200; i++)
        {
            persons.Add(new Person
            {
                Id = i + 10, // Offset to avoid conflicts
                Name = $"Person {i}",
                Email = $"person{i}@example.com",
                BirthInfoId = i <= 100 ? i + 10 : null // First 100 have birth info
            });
        }

        // Create addresses (2-3 per person for first 150 persons)
        int addressId = 10;
        for (int personId = 11; personId <= 160; personId++)
        {
            int addressCount = (personId % 3) + 1; // 1-3 addresses per person
            for (int j = 0; j < addressCount; j++)
            {
                addresses.Add(new Address
                {
                    Id = addressId++,
                    PersonId = personId,
                    Street = $"{100 + j} Street {personId}",
                    City = j == 0 ? "Kyiv" : j == 1 ? "Lviv" : "Odesa",
                    Country = "Ukraine",
                    IsActive = j == 0 // First address is active
                });
            }
        }

        // Create orders (0-10 per person for first 150 persons)
        int orderId = 10;
        for (int personId = 11; personId <= 160; personId++)
        {
            int orderCount = personId % 11; // 0-10 orders per person
            for (int j = 0; j < orderCount; j++)
            {
                orders.Add(new Order
                {
                    Id = orderId++,
                    PersonId = personId,
                    OrderNumber = $"ORD{orderId:D6}",
                    Amount = 50m + ((orderId * 13) % 500), // Amounts between 50-550
                    OrderDate = DateTime.Now.AddDays(-(j * 5 + personId % 30)),
                    IsCompleted = j % 3 != 0 // ~67% completed
                });
            }
        }

        _context.PersonBirthInfos.AddRange(birthInfos);
        _context.Persons.AddRange(persons);
        _context.Addresses.AddRange(addresses);
        _context.Orders.AddRange(orders);

        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task EfCore_Expression_Performance_Should_Be_Reasonable()
    {
        // Arrange
        var stopwatch = new Stopwatch();

        // Test simple expression performance
        stopwatch.Start();
        var names = await _context.Persons
            .Select(EfCoreMapper.GetPersonNameExpression())
            .ToListAsync();
        stopwatch.Stop();
        var simpleExpressionTime = stopwatch.ElapsedMilliseconds;

        await Assert.That(names.Count).IsGreaterThan(200);
        await Assert.That(simpleExpressionTime).IsLessThan(1000);

        // Test complex expression performance
        stopwatch.Restart();
        var summaries = await _context.Persons
            .Include(p => p.BirthInfo)
            .Select(EfCoreMapper.GetPersonSummaryExpression())
            .ToListAsync();
        stopwatch.Stop();
        var complexExpressionTime = stopwatch.ElapsedMilliseconds;

        await Assert.That(summaries.Count).IsGreaterThan(200);
        await Assert.That(complexExpressionTime).IsLessThan(2000);

        // Output performance metrics for debugging
        Console.WriteLine($"Simple expression: {simpleExpressionTime}ms for {names.Count} records");
        Console.WriteLine($"Complex expression: {complexExpressionTime}ms for {summaries.Count} records");
    }

    [Test]
    public async Task EfCore_Large_Dataset_Expressions_Should_Work()
    {
        // Test various expressions on the large dataset
        var ageExpressions = await _context.Persons
            .Select(EfCoreMapper.GetPersonAgeExpression())
            .ToListAsync();

        var birthPlaceExpressions = await _context.Persons
            .Select(EfCoreMapper.GetBirthPlaceExpression())
            .ToListAsync();

        var isAdultExpressions = await _context.Persons
            .Select(EfCoreMapper.IsAdultExpression())
            .ToListAsync();

        // Verify counts
        await Assert.That(ageExpressions.Count).IsGreaterThan(200);
        await Assert.That(birthPlaceExpressions.Count).IsGreaterThan(200);
        await Assert.That(isAdultExpressions.Count).IsGreaterThan(200);

        // Verify data quality
        var agesWithValues = ageExpressions.Where(a => a.HasValue).Count();
        var agesWithoutValues = ageExpressions.Where(a => !a.HasValue).Count();
        await Assert.That(agesWithValues).IsGreaterThanOrEqualTo(100); // At least 100 with ages
        await Assert.That(agesWithoutValues).IsGreaterThanOrEqualTo(100); // At least 100 without ages

        await Assert.That(birthPlaceExpressions).Contains("Kyiv");
        await Assert.That(birthPlaceExpressions).Contains("Lviv");
        await Assert.That(birthPlaceExpressions).Contains("New York");
        await Assert.That(birthPlaceExpressions).Contains("Unknown");

        // All persons with birth info should be adults (ages 18+)
        await Assert.That(isAdultExpressions.Count(adult => adult)).IsGreaterThanOrEqualTo(100);
    }

    [Test]
    public async Task EfCore_Filtered_Expressions_Should_Work()
    {
        // Test expressions with various filters

        // Get only adults from Ukraine
        var ukrainianAdults = await _context.Persons
            .Include(p => p.BirthInfo)
            .Where(p => p.BirthInfo != null &&
                       p.BirthInfo.Address.Contains("Ukraine") &&
                       p.BirthInfo.Age >= 18)
            .Select(p => new
            {
                Name = EfCoreMapper.GetPersonName(p),
                Age = EfCoreMapper.GetPersonAge(p),
                BirthPlace = EfCoreMapper.GetBirthPlace(p),
                Summary = EfCoreMapper.GetPersonSummary(p)
            })
            .ToListAsync();

        await Assert.That(ukrainianAdults.Count).IsGreaterThan(0);
        foreach (var person in ukrainianAdults)
        {
            await Assert.That(person.Age!.Value).IsGreaterThanOrEqualTo(18);
            await Assert.That(person.BirthPlace == "Kyiv" || person.BirthPlace == "Lviv").IsTrue();
        }

        // Get VIP customers
        var vipCustomers = await _context.Persons
            .Include(p => p.Orders)
            .Include(p => p.BirthInfo)
            .Where(EfCoreMapper.IsVipCustomerExpression())
            .Select(p => new
            {
                Name = EfCoreMapper.GetPersonName(p),
                OrderCount = EfCoreMapper.GetOrderCount(p),
                TotalAmount = EfCoreMapper.GetTotalOrderAmount(p)
            })
            .ToListAsync();

        // Verify VIP criteria
        foreach (var customer in vipCustomers)
        {
            await Assert.That(customer.TotalAmount).IsGreaterThanOrEqualTo(1000m);
        }
    }

    [Test]
    public async Task EfCore_Pagination_With_Expressions_Should_Work()
    {
        // Test pagination scenarios
        const int pageSize = 20;

        for (int page = 0; page < 3; page++)
        {
            var pagedResults = await _context.Persons
                .OrderBy(p => p.Id)
                .Skip(page * pageSize)
                .Take(pageSize)
                .Select(EfCoreMapper.GetPersonComplexExpression())
                .ToListAsync();

            await Assert.That(pagedResults.Count).IsLessThanOrEqualTo(pageSize);
            await Assert.That(pagedResults.Count > 0 || page >= 10).IsTrue(); // Should have data for first 10+ pages

            // Verify expressions work correctly in pagination
            foreach (var result in pagedResults)
            {
                await Assert.That(result.Name).IsNotNull();
                await Assert.That(result.BirthPlace).IsNotNull();
                await Assert.That(result.PersonCategory).IsNotNull();
                await Assert.That(result.AddressCount).IsGreaterThanOrEqualTo(0);
                await Assert.That(result.OrderCount).IsGreaterThanOrEqualTo(0);
            }
        }
    }

    [Test]
    public async Task EfCore_Aggregation_With_Expressions_Should_Work()
    {
        // Test various aggregations using expressions

        // Count adults
        var adultCount = await _context.Persons
            .CountAsync(EfCoreMapper.IsAdultExpression());

        // Average age of persons with birth info
        var averageAge = await _context.Persons
            .Where(p => p.BirthInfo != null)
            .Select(EfCoreMapper.GetPersonAgeExpression())
            .Where(age => age.HasValue)
            .AverageAsync(age => age!.Value);

        // Total order amounts by VIP customers
        var vipTotalAmount = await _context.Persons
            .Include(p => p.Orders)
            .Include(p => p.BirthInfo)
            .Where(EfCoreMapper.IsVipCustomerExpression())
            .Select(EfCoreMapper.GetTotalOrderAmountExpression())
            .SumAsync();

        // Group by birth place and count
        var birthPlaceCounts = await _context.Persons
            .Include(p => p.BirthInfo)
            .Select(EfCoreMapper.GetBirthPlaceExpression())
            .GroupBy(place => place)
            .Select(g => new { BirthPlace = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        // Assertions
        await Assert.That(adultCount).IsGreaterThan(0);
        await Assert.That(averageAge).IsGreaterThan(18);
        await Assert.That(vipTotalAmount).IsGreaterThanOrEqualTo(0);
        await Assert.That(birthPlaceCounts.Count).IsGreaterThanOrEqualTo(3); // Should have at least Kyiv, Lviv, New York, Unknown

        var unknownCount = birthPlaceCounts.FirstOrDefault(x => x.BirthPlace == "Unknown")?.Count ?? 0;
        await Assert.That(unknownCount).IsGreaterThanOrEqualTo(100); // At least 100 persons without birth info
    }
}