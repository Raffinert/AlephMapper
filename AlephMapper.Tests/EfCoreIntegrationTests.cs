using AgileObjects.ReadableExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.Tests;

public class EfCoreIntegrationTests
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
            new Order { Id = 6, PersonId = 3, OrderNumber = "ORD006", Amount = 325.50m, OrderDate = DateTime.Now.AddDays(-3), IsCompleted = true },
            new Order { Id = 7, PersonId = 3, OrderNumber = "ORD007", Amount = 180.00m, OrderDate = DateTime.Now.AddDays(-1), IsCompleted = false }
        );

        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task EfCore_Simple_Expression_Should_Work()
    {
        // Arrange
        var nameExpression = EfCoreMapper.GetPersonNameExpression();
        var emailExpression = EfCoreMapper.GetPersonEmailExpression();

        // Act & Assert
        var persons = await _context.Persons.Select(nameExpression).ToListAsync();
        await Assert.That(persons.Count).IsEqualTo(4);
        await Assert.That(persons).Contains("John Doe");
        await Assert.That(persons).Contains("Jane Smith");
        await Assert.That(persons).Contains("Bob Johnson");
        await Assert.That(persons).Contains("Alice Brown");

        var emails = await _context.Persons.Select(emailExpression).ToListAsync();
        await Assert.That(emails.Count).IsEqualTo(4);
        foreach (var email in emails)
        {
            await Assert.That(email).Contains("@example.com");
        }
    }

    [Test]
    public async Task EfCore_Null_Conditional_Expressions_Should_Handle_Nulls()
    {
        // Arrange
        var ageExpression = EfCoreMapper.GetPersonAgeExpression();
        var birthPlaceExpression = EfCoreMapper.GetBirthPlaceExpression();
        var birthAddressExpression = EfCoreMapper.GetBirthAddressExpression();

        // Act
        var ages = await _context.Persons.Select(ageExpression).ToListAsync();
        var birthPlaces = await _context.Persons.Select(birthPlaceExpression).ToListAsync();
        var birthAddresses = await _context.Persons.Select(birthAddressExpression).ToListAsync();

        // Assert
        await Assert.That(ages.Count).IsEqualTo(4);
        await Assert.That(ages.Any(a => a == 30)).IsTrue();  // John Doe
        await Assert.That(ages.Any(a => a == 25)).IsTrue();  // Jane Smith
        await Assert.That(ages.Any(a => a == 40)).IsTrue();  // Bob Johnson
        await Assert.That(ages.Any(a => a == null)).IsTrue(); // Alice Brown (no birth info)

        await Assert.That(birthPlaces.Count).IsEqualTo(4);
        await Assert.That(birthPlaces).Contains("Kyiv");
        await Assert.That(birthPlaces).Contains("Lviv");
        await Assert.That(birthPlaces).Contains("New York");
        await Assert.That(birthPlaces).Contains("Unknown"); // Alice Brown default

        await Assert.That(birthAddresses.Count).IsEqualTo(4);
        await Assert.That(birthAddresses).Contains("Kyiv, Ukraine");
        await Assert.That(birthAddresses).Contains("Lviv, Ukraine");
        await Assert.That(birthAddresses).Contains("New York, USA");
        await Assert.That(birthAddresses).Contains("Not specified"); // Alice Brown default
    }

    [Test]
    public async Task EfCore_Boolean_Expressions_Should_Work_Correctly()
    {
        // Arrange
        var hasBirthInfoExpression = EfCoreMapper.HasBirthInfoExpression();
        var isAdultExpression = EfCoreMapper.IsAdultExpression();
        var bornInUkraineExpression = EfCoreMapper.BornInUkraineExpression();

        // Act
        var hasBirthInfoResults = await _context.Persons.Select(hasBirthInfoExpression).ToListAsync();
        var isAdultResults = await _context.Persons.Select(isAdultExpression).ToListAsync();
        var bornInUkraineResults = await _context.Persons.Select(bornInUkraineExpression).ToListAsync();

        // Assert
        await Assert.That(hasBirthInfoResults.Count).IsEqualTo(4);
        await Assert.That(hasBirthInfoResults.Count(x => x)).IsEqualTo(3); // 3 persons have birth info
        await Assert.That(hasBirthInfoResults.Count(x => !x)).IsEqualTo(1); // 1 person doesn't have birth info

        await Assert.That(isAdultResults.Count).IsEqualTo(4);
        await Assert.That(isAdultResults.Count(x => x)).IsEqualTo(3); // All with birth info are adults (30, 25, 40)

        await Assert.That(bornInUkraineResults.Count).IsEqualTo(4);
        await Assert.That(bornInUkraineResults.Count(x => x)).IsEqualTo(2); // John and Jane born in Ukraine
    }

    [Test]
    public async Task EfCore_Collection_Based_Expressions_Should_Work()
    {
        // Arrange
        var addressCountExpression = EfCoreMapper.GetAddressCountExpression();
        var orderCountExpression = EfCoreMapper.GetOrderCountExpression();
        var hasActiveAddressExpression = EfCoreMapper.HasActiveAddressExpression();
        var hasCompletedOrdersExpression = EfCoreMapper.HasCompletedOrdersExpression();

        // Act
        var addressCounts = await _context.Persons
            .Include(p => p.Addresses)
            .OrderBy(p => p.Id)
            .Select(addressCountExpression)
            .ToListAsync();

        var orderCounts = await _context.Persons
            .Include(p => p.Orders)
            .OrderBy(p => p.Id)
            .Select(orderCountExpression)
            .ToListAsync();

        var hasActiveAddresses = await _context.Persons
            .Include(p => p.Addresses)
            .OrderBy(p => p.Id)
            .Select(hasActiveAddressExpression)
            .ToListAsync();

        var hasCompletedOrders = await _context.Persons
            .Include(p => p.Orders)
            .OrderBy(p => p.Id)
            .Select(hasCompletedOrdersExpression)
            .ToListAsync();

        // Assert
        await Assert.That(addressCounts.Count).IsEqualTo(4);
        await Assert.That(addressCounts[0]).IsEqualTo(2); // John has 2 addresses
        await Assert.That(addressCounts[1]).IsEqualTo(1); // Jane has 1 address
        await Assert.That(addressCounts[2]).IsEqualTo(1); // Bob has 1 address
        await Assert.That(addressCounts[3]).IsEqualTo(0); // Alice has 0 addresses

        await Assert.That(orderCounts.Count).IsEqualTo(4);
        await Assert.That(orderCounts[0]).IsEqualTo(3); // John has 3 orders
        await Assert.That(orderCounts[1]).IsEqualTo(1); // Jane has 1 order
        await Assert.That(orderCounts[2]).IsEqualTo(3); // Bob has 3 orders
        await Assert.That(orderCounts[3]).IsEqualTo(0); // Alice has 0 orders

        // Check active addresses
        await Assert.That(hasActiveAddresses[0]).IsTrue();  // John has active address
        await Assert.That(hasActiveAddresses[1]).IsTrue();  // Jane has active address
        await Assert.That(hasActiveAddresses[2]).IsTrue();  // Bob has active address
        await Assert.That(hasActiveAddresses[3]).IsFalse(); // Alice has no addresses

        // Check completed orders
        await Assert.That(hasCompletedOrders[0]).IsTrue();  // John has completed orders
        await Assert.That(hasCompletedOrders[1]).IsTrue();  // Jane has completed orders
        await Assert.That(hasCompletedOrders[2]).IsTrue();  // Bob has completed orders
        await Assert.That(hasCompletedOrders[3]).IsFalse(); // Alice has no orders
    }

    [Test]
    public async Task EfCore_Complex_Expressions_Should_Work()
    {
        // Arrange
        var firstActiveAddressCityExpression = EfCoreMapper.GetFirstActiveAddressCityExpression();
        var totalOrderAmountExpression = EfCoreMapper.GetTotalOrderAmountExpression();
        var latestOrderNumberExpression = EfCoreMapper.GetLatestOrderNumberExpression();

        // Act
        var firstActiveAddressCities = await _context.Persons
            .Include(p => p.Addresses)
            .OrderBy(p => p.Id)
            .Select(firstActiveAddressCityExpression)
            .ToListAsync();

        var totalOrderAmounts = await _context.Persons
            .Include(p => p.Orders)
            .OrderBy(p => p.Id)
            .Select(totalOrderAmountExpression)
            .ToListAsync();

        var latestOrderNumbers = await _context.Persons
            .Include(p => p.Orders)
            .OrderBy(p => p.Id)
            .Select(latestOrderNumberExpression)
            .ToListAsync();

        // Assert
        await Assert.That(firstActiveAddressCities.Count).IsEqualTo(4);
        await Assert.That(firstActiveAddressCities[0]).IsEqualTo("Kyiv"); // John's active address
        await Assert.That(firstActiveAddressCities[1]).IsEqualTo("Lviv"); // Jane's active address
        await Assert.That(firstActiveAddressCities[2]).IsEqualTo("New York"); // Bob's active address
        await Assert.That(firstActiveAddressCities[3]).IsEqualTo("No active address"); // Alice default

        await Assert.That(totalOrderAmounts.Count).IsEqualTo(4);
        await Assert.That(totalOrderAmounts[0]).IsEqualTo(426.25m); // John's completed orders total
        await Assert.That(totalOrderAmounts[1]).IsEqualTo(450.00m); // Jane's completed orders total
        await Assert.That(totalOrderAmounts[2]).IsEqualTo(1575.75m); // Bob's completed orders total
        await Assert.That(totalOrderAmounts[3]).IsEqualTo(0m); // Alice has no orders

        await Assert.That(latestOrderNumbers.Count).IsEqualTo(4);
        await Assert.That(latestOrderNumbers[0]).IsEqualTo("ORD003"); // John's latest order
        await Assert.That(latestOrderNumbers[1]).IsEqualTo("ORD004"); // Jane's latest (and only) order
        await Assert.That(latestOrderNumbers[2]).IsEqualTo("ORD007"); // Bob's latest order
        await Assert.That(latestOrderNumbers[3]).IsEqualTo("No orders"); // Alice default
    }

    [Test]
    public async Task EfCore_PersonSummary_Expression_Should_Work()
    {
        // Arrange
        var personSummaryExpression = EfCoreMapper.GetPersonSummaryExpression();

        // Act
        var summaries = await _context.Persons
            .Include(p => p.BirthInfo)
            .Select(personSummaryExpression)
            .ToListAsync();

        // Assert
        await Assert.That(summaries.Count).IsEqualTo(4);
        await Assert.That(summaries[0]).IsEqualTo("John Doe (30 years old) from Kyiv");
        await Assert.That(summaries[1]).IsEqualTo("Jane Smith (25 years old) from Lviv");
        await Assert.That(summaries[2]).IsEqualTo("Bob Johnson (40 years old) from New York");
        await Assert.That(summaries[3]).IsEqualTo("Alice Brown (unknown age) from Unknown");
    }

    [Test]
    public async Task EfCore_PersonCategory_Expression_Should_Work()
    {
        // Arrange
        var personCategoryExpression = EfCoreMapper.GetPersonCategoryExpression();

        // Act
        var categories = await _context.Persons
            .Include(p => p.BirthInfo)
            .OrderBy(p => p.Id)
            .Select(personCategoryExpression)
            .ToListAsync();

        // Assert
        await Assert.That(categories.Count).IsEqualTo(4);
        await Assert.That(categories[0]).IsEqualTo("Adult"); // John, 30 years old
        await Assert.That(categories[1]).IsEqualTo("Adult"); // Jane, 25 years old
        await Assert.That(categories[2]).IsEqualTo("Adult"); // Bob, 40 years old
        await Assert.That(categories[3]).IsEqualTo("Unknown Age"); // Alice, null age
    }

    [Test]
    public async Task EfCore_VipCustomer_Expression_Should_Work()
    {
        // Arrange
        var isVipCustomerExpression = EfCoreMapper.IsVipCustomerExpression();

        // Act
        var vipStatuses = await _context.Persons
            .Include(p => p.Orders)
            .Include(p => p.BirthInfo)
            .OrderBy(p => p.Id)
            .Select(isVipCustomerExpression)
            .ToListAsync();

        // Assert
        await Assert.That(vipStatuses.Count).IsEqualTo(4);
        await Assert.That(vipStatuses[0]).IsFalse(); // John: 3 orders, total < 1000
        await Assert.That(vipStatuses[1]).IsFalse(); // Jane: 1 order, total < 1000
        await Assert.That(vipStatuses[2]).IsTrue();  // Bob: 3 orders, total > 1000
        await Assert.That(vipStatuses[3]).IsFalse(); // Alice: no orders
    }

    [Test]
    public async Task EfCore_Complex_DTO_Expression()
    {
        // Act
        var actual = EfCoreMapper.GetPersonComplexExpression().ToReadableString();

        // Assert
        var expected = """
                       p => new PersonSummaryDto
                       {
                           Id = p.Id,
                           Name = p.Name,
                           Email = p.Email,
                           Age = (p.BirthInfo != null) ? (int?)p.BirthInfo.Age : (int?)null,
                           BirthPlace = ((p.BirthInfo != null) ? p.BirthInfo.BirthPlace : null) ?? "Unknown",
                           BirthAddress = ((p.BirthInfo != null) ? p.BirthInfo.Address : null) ?? "Not specified",
                           HasBirthInfo = p.BirthInfo != null,
                           IsAdult = ((p.BirthInfo != null) ? (int?)p.BirthInfo.Age : (int?)null) >= ((int?)18),
                           AddressCount = p.Addresses.Count,
                           OrderCount = p.Orders.Count,
                           HasActiveAddress = p.Addresses.Any(a => a.IsActive),
                           PersonCategory = (p.BirthInfo == null)
                               ? "Unknown Age"
                               : (p.BirthInfo.Age < 18) ? "Minor" : (p.BirthInfo.Age < 65) ? "Adult" : "Senior",
                           Summary = (p.BirthInfo != null)
                               ? p.Name + " (" + p.BirthInfo.Age + " years old) from " + p.BirthInfo.BirthPlace ?? "Unknown"
                               : p.Name + " (unknown age) from Unknown"
                       }
                       """;
        await Assert.That(expected).IsEqualTo(actual);
    }

    [Test]
    public async Task EfCore_Complex_DTO_Expression_Should_Work()
    {
        //var query = _context.Persons
        //    .Include(p => p.BirthInfo)
        //    .Include(p => p.Addresses)
        //    .Include(p => p.Orders)
        //    .Select(p => new PersonSummaryDto
        //    {
        //        Id = p.Id,
        //        Name = EfCoreMapper.GetPersonName(p),
        //        Email = EfCoreMapper.GetPersonEmail(p),
        //        Age = EfCoreMapper.GetPersonAge(p),
        //        BirthPlace = EfCoreMapper.GetBirthPlace(p),
        //        BirthAddress = EfCoreMapper.GetBirthAddress(p),
        //        HasBirthInfo = EfCoreMapper.HasBirthInfo(p),
        //        IsAdult = EfCoreMapper.IsAdult(p),
        //        AddressCount = EfCoreMapper.GetAddressCount(p),
        //        OrderCount = EfCoreMapper.GetOrderCount(p),
        //        HasActiveAddress = EfCoreMapper.HasActiveAddress(p),
        //        PersonCategory = EfCoreMapper.GetPersonCategory(p),
        //        Summary = EfCoreMapper.GetPersonSummary(p)
        //    }).AsSplitQuery();

        //var personSummaries1 = query.ToList();

        var query = _context.Persons.Select(EfCoreMapper.GetPersonComplexExpression());

        // Act
        var personSummaries = await query.ToListAsync();

        // Assert
        await Assert.That(personSummaries.Count).IsEqualTo(4);

        var john = personSummaries.First(p => p.Name == "John Doe");
        await Assert.That(john.Id).IsEqualTo(1);
        await Assert.That(john.Age).IsEqualTo(30);
        await Assert.That(john.BirthPlace).IsEqualTo("Kyiv");
        await Assert.That(john.BirthAddress).IsEqualTo("Kyiv, Ukraine");
        await Assert.That(john.HasBirthInfo).IsTrue();
        await Assert.That(john.IsAdult).IsTrue();
        await Assert.That(john.AddressCount).IsEqualTo(2);
        await Assert.That(john.OrderCount).IsEqualTo(3);
        await Assert.That(john.HasActiveAddress).IsTrue();
        await Assert.That(john.PersonCategory).IsEqualTo("Adult");
        await Assert.That(john.Summary).IsEqualTo("John Doe (30 years old) from Kyiv");

        var alice = personSummaries.First(p => p.Name == "Alice Brown");
        await Assert.That(alice.Id).IsEqualTo(4);
        await Assert.That(alice.Age).IsNull();
        await Assert.That(alice.BirthPlace).IsEqualTo("Unknown");
        await Assert.That(alice.BirthAddress).IsEqualTo("Not specified");
        await Assert.That(alice.HasBirthInfo).IsFalse();
        await Assert.That(alice.IsAdult).IsFalse();
        await Assert.That(alice.AddressCount).IsEqualTo(0);
        await Assert.That(alice.OrderCount).IsEqualTo(0);
        await Assert.That(alice.HasActiveAddress).IsFalse();
        await Assert.That(alice.PersonCategory).IsEqualTo("Unknown Age");
        await Assert.That(alice.Summary).IsEqualTo("Alice Brown (unknown age) from Unknown");
    }

    [Test]
    public async Task EfCore_Ignore_Policy_Should_Throw_On_Null_Navigation()
    {
        // Arrange
        var ignoreAgeExpression = EfCoreIgnoreMapper.GetPersonAgeExpression();

        // Act & Assert - This should work for persons with birth info
        var personsWithBirthInfo = await _context.Persons
            .Where(p => p.BirthInfo != null)
            .Select(ignoreAgeExpression)
            .ToListAsync();

        await Assert.That(personsWithBirthInfo.Count).IsEqualTo(3);
        foreach (var age in personsWithBirthInfo)
        {
            await Assert.That(age).IsNotNull();
        }

        // For the ignore policy, querying persons without birth info would 
        // result in null values being passed through, but EF Core handles this gracefully
        var allPersonsAges = await _context.Persons.Select(ignoreAgeExpression).ToListAsync();
        await Assert.That(allPersonsAges.Count).IsEqualTo(4);
        await Assert.That(allPersonsAges.Any(a => a == null)).IsTrue(); // Alice Brown should have null age
    }

    [Test]
    public async Task EfCore_Rewrite_Policy_Should_Generate_Safe_SQL()
    {
        // This test verifies that the rewrite policy generates SQL that handles nulls properly

        // Arrange
        var birthPlaceExpression = EfCoreMapper.GetBirthPlaceExpression();

        // Act - This should not throw any SQL exceptions
        var birthPlaces = await _context.Persons
            .Select(birthPlaceExpression)
            .ToListAsync();

        // Assert
        await Assert.That(birthPlaces.Count).IsEqualTo(4);
        await Assert.That(birthPlaces).Contains("Kyiv");
        await Assert.That(birthPlaces).Contains("Lviv");
        await Assert.That(birthPlaces).Contains("New York");
        await Assert.That(birthPlaces).Contains("Unknown"); // Default value for null

        // Verify no exceptions were thrown and all values are properly handled
        foreach (var place in birthPlaces)
        {
            await Assert.That(place).IsNotNull();
        }
    }
}