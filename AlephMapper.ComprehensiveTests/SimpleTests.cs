using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AgileObjects.ReadableExpressions;

namespace AlephMapper.ComprehensiveTests;

public class SimpleIntegrationTests
{
    private SqliteConnection _connection = null!;
    private ComprehensiveTestDbContext _context = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ComprehensiveTestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    #region Expressive Tests

    [Test]
    public async Task Simple_Property_Expressions_Should_Work()
    {
        // Arrange
        var fullNameExpression = SimpleEmployeeMapper.GetFullNameExpression();
        var emailExpression = SimpleEmployeeMapper.GetEmailExpression();

        // Act
        var fullNames = await _context.Employees
            .Select(fullNameExpression)
            .ToListAsync();

        var emails = await _context.Employees
            .Select(emailExpression)
            .ToListAsync();

        // Assert
        await Assert.That(fullNames.Count).IsEqualTo(6);
        await Assert.That(fullNames).Contains("John Doe");
        await Assert.That(fullNames).Contains("Jane Smith");

        await Assert.That(emails.Count).IsEqualTo(6);
        await Assert.That(emails).Contains("john.doe@company.com");
    }

    [Test]
    public async Task Null_Conditional_Expressions_Should_Work()
    {
        // Arrange
        var departmentExpression = SimpleEmployeeMapper.GetDepartmentNameExpression();
        var managerExpression = SimpleEmployeeMapper.GetManagerNameExpression();
        var phoneExpression = SimpleEmployeeMapper.GetPhoneExpression();

        // Act
        var departments = await _context.Employees
            .Select(departmentExpression)
            .ToListAsync();

        var managers = await _context.Employees
            .Select(managerExpression)
            .ToListAsync();

        var phones = await _context.Employees
            .Select(phoneExpression)
            .ToListAsync();

        // Assert
        await Assert.That(departments.Count).IsEqualTo(6);
        await Assert.That(departments).Contains("Engineering");
        await Assert.That(departments).Contains("No Department");

        await Assert.That(managers.Count).IsEqualTo(6);
        await Assert.That(managers).Contains("John"); // John is Jane's manager
        await Assert.That(managers).Contains("No Manager");

        await Assert.That(phones.Count).IsEqualTo(6);
        await Assert.That(phones).Contains("+1-555-0101");
        await Assert.That(phones).Contains("No Phone");
    }

    [Test]
    public async Task Boolean_Expressions_Should_Work()
    {
        // Arrange
        var hasProfileExpression = SimpleEmployeeMapper.HasProfileExpression();
        var isActiveExpression = SimpleEmployeeMapper.IsActiveExpression();

        // Act
        var hasProfiles = await _context.Employees
            .Select(hasProfileExpression)
            .ToListAsync();

        var isActives = await _context.Employees
            .Select(isActiveExpression)
            .ToListAsync();

        // Assert
        await Assert.That(hasProfiles.Count).IsEqualTo(6);
        await Assert.That(hasProfiles.Count(h => h)).IsEqualTo(4); // 4 employees have profiles

        await Assert.That(isActives.Count).IsEqualTo(6);
        await Assert.That(isActives.Count(a => a)).IsEqualTo(5); // 5 employees are active
    }

    [Test]
    public async Task Collection_Count_Should_Work()
    {
        // Arrange
        var addressCountExpression = SimpleEmployeeMapper.GetAddressCountExpression();

        // Seed some addresses first
        _context.EmployeeAddresses.AddRange(
            new EmployeeAddress { Id = 100, EmployeeId = 1, Street = "123 Main St", City = "Seattle", Country = "USA", IsPrimary = true },
            new EmployeeAddress { Id = 101, EmployeeId = 1, Street = "456 Work Ave", City = "Seattle", Country = "USA", IsPrimary = false },
            new EmployeeAddress { Id = 102, EmployeeId = 2, Street = "789 Pine Rd", City = "Portland", Country = "USA", IsPrimary = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var addressCounts = await _context.Employees
            .OrderBy(e => e.Id)
            .Select(addressCountExpression)
            .ToListAsync();

        // Assert
        await Assert.That(addressCounts.Count).IsEqualTo(6);
        await Assert.That(addressCounts[0]).IsEqualTo(2); // John has 2 addresses
        await Assert.That(addressCounts[1]).IsEqualTo(1); // Jane has 1 address
        await Assert.That(addressCounts[2]).IsEqualTo(0); // Bob has no addresses
    }

    [Test]
    public async Task Complex_DTO_Expression_Should_Work()
    {
        // Arrange
        var simpleDtoExpression = SimpleEmployeeMapper.MapToSimpleDtoExpression();

        // Act
        var simpleDtos = await _context.Employees
            .Select(simpleDtoExpression)
            .ToListAsync();

        // Assert
        await Assert.That(simpleDtos.Count).IsEqualTo(6);

        var john = simpleDtos.First(d => d.FirstName == "John");
        await Assert.That(john.Id).IsEqualTo(1);
        await Assert.That(john.LastName).IsEqualTo("Doe");
        await Assert.That(john.Email).IsEqualTo("john.doe@company.com");
        await Assert.That(john.DepartmentName).IsEqualTo("Engineering");

        var alice = simpleDtos.First(d => d.FirstName == "Alice");
        await Assert.That(alice.DepartmentName).IsEqualTo("No Department");
    }

    [Test]
    public async Task Ignore_Policy_Should_Work()
    {
        // Arrange
        var ignoreFullNameExpression = SimpleIgnoreMapper.GetFullNameExpression();
        var ignoreDtoExpression = SimpleIgnoreMapper.MapToSimpleDtoExpression();

        // Act
        var fullNames = await _context.Employees
            .Select(ignoreFullNameExpression)
            .ToListAsync();

        var dtos = await _context.Employees
            .Select(ignoreDtoExpression)
            .ToListAsync();

        // Assert
        await Assert.That(fullNames.Count).IsEqualTo(6);
        await Assert.That(fullNames).Contains("John Doe");

        await Assert.That(dtos.Count).IsEqualTo(6);
        await Assert.That(dtos.First(d => d.FirstName == "John").DepartmentName).IsEqualTo("Engineering");
    }

    #endregion

    #region Expression Tree Validation

    [Test]
    public async Task Expression_Trees_Should_Have_Correct_Structure()
    {
        // Arrange
        var fullNameExpression = SimpleEmployeeMapper.GetFullNameExpression();
        var dtoExpression = SimpleEmployeeMapper.MapToSimpleDtoExpression();

        // Act
        var fullNameString = fullNameExpression.ToReadableString();
        var dtoString = dtoExpression.ToReadableString();

        // Assert
        await Assert.That(fullNameString).Contains("FirstName");
        await Assert.That(fullNameString).Contains("LastName");
        // Check for string interpolation or concat rather than specifically "+"
        await Assert.That(fullNameString.Contains("Format") || fullNameString.Contains("+")).IsTrue();

        await Assert.That(dtoString).Contains("new EmployeeSimpleDto");
        await Assert.That(dtoString).Contains("FirstName");
        await Assert.That(dtoString).Contains("Email");
    }

    #endregion
}

public class SimpleUpdateTests
{
    [Test]
    public async Task Simple_Update_Should_Work()
    {
        // Arrange
        var employee = new Employee
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Department = new Department { Name = "Engineering" }
        };

        var target = new EmployeeSimpleDto();

        // Act
        var result = SimpleUpdateMapper.MapToSimpleDto(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.FirstName).IsEqualTo("John");
        await Assert.That(target.LastName).IsEqualTo("Doe");
        await Assert.That(target.Email).IsEqualTo("john@example.com");
        await Assert.That(target.DepartmentName).IsEqualTo("Engineering");
    }

    [Test]
    public async Task Update_Should_Handle_Nulls()
    {
        // Arrange
        var employee = new Employee
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Department = null
        };

        var target = new EmployeeSimpleDto();

        // Act
        var result = SimpleUpdateMapper.MapToSimpleDto(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.DepartmentName).IsEqualTo("No Department");
    }

    [Test]
    public async Task Department_Update_Should_Work()
    {
        // Arrange
        var department = new Department
        {
            Id = 1,
            Name = "Engineering",
            Description = "Software Development",
            IsActive = true
        };

        var target = new DepartmentUpdateDto();

        // Act
        var result = SimpleUpdateMapper.MapToDepartmentDto(department, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.Name).IsEqualTo("Engineering");
        await Assert.That(target.Description).IsEqualTo("Software Development");
        await Assert.That(target.IsActive).IsTrue();
    }
}