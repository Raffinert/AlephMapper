using AgileObjects.ReadableExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class MultiParamIntegrationTests
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

    #region Multi-Param Expressive Tests

    [Test]
    public async Task MultiParam_FormatName_Expression_Should_Work_In_EFCore_Query()
    {
        // Arrange — MapToDto uses FormatName(first, last) which should be inlined
        var expression = MultiParamEmployeeMapper.MapToDtoExpression();

        // Act
        var dtos = await _context.Employees
            .Select(expression)
            .ToListAsync();

        // Assert
        await Assert.That(dtos.Count).IsEqualTo(6);

        var john = dtos.First(d => d.Id == 1);
        await Assert.That(john.FullName).IsEqualTo("John Doe");
        await Assert.That(john.Email).IsEqualTo("john.doe@company.com");
        await Assert.That(john.DepartmentName).IsEqualTo("Engineering");
        await Assert.That(john.IsActive).IsTrue();

        var jane = dtos.First(d => d.Id == 2);
        await Assert.That(jane.FullName).IsEqualTo("Jane Smith");
    }

    [Test]
    public async Task MultiParam_Nested_FormatNameWithEmail_Expression_Should_Work()
    {
        // Arrange — MapToDtoWithEmail uses FormatNameWithEmail(first, last, email)
        //           which internally calls FormatName(first, last) — nested multi-param
        var expression = MultiParamEmployeeMapper.MapToDtoWithEmailExpression();

        // Act
        var dtos = await _context.Employees
            .Select(expression)
            .ToListAsync();

        // Assert
        await Assert.That(dtos.Count).IsEqualTo(6);

        var john = dtos.First(d => d.Id == 1);
        await Assert.That(john.FullName).IsEqualTo("John Doe <john.doe@company.com>");
    }

    [Test]
    public async Task MultiParam_SimpleDto_Expression_Should_Work()
    {
        // Arrange
        var expression = MultiParamEmployeeMapper.MapToSimpleDtoExpression();

        // Act
        var dtos = await _context.Employees
            .Select(expression)
            .ToListAsync();

        // Assert
        await Assert.That(dtos.Count).IsEqualTo(6);

        var john = dtos.First(d => d.Id == 1);
        await Assert.That(john.FirstName).IsEqualTo("John");
        await Assert.That(john.LastName).IsEqualTo("Doe");
        await Assert.That(john.DepartmentName).IsEqualTo("Engineering");

        // Alice has no department
        var alice = dtos.First(d => d.FirstName == "Alice");
        await Assert.That(alice.DepartmentName).IsEqualTo("Unassigned");
    }

    #endregion

    #region Named Argument Tests

    [Test]
    public async Task NamedArgs_Should_Map_Correctly_In_Expression()
    {
        // Arrange — FormatName(last: ..., first: ...) with reversed named arguments
        var expression = NamedArgEmployeeMapper.MapToDtoExpression();

        // Act
        var dtos = await _context.Employees
            .Select(expression)
            .ToListAsync();

        // Assert — even though args are reversed, result should be first + " " + last
        await Assert.That(dtos.Count).IsEqualTo(6);

        var john = dtos.First(d => d.Id == 1);
        await Assert.That(john.FullName).IsEqualTo("John Doe");

        var jane = dtos.First(d => d.Id == 2);
        await Assert.That(jane.FullName).IsEqualTo("Jane Smith");
    }

    #endregion

    #region Multi-Param Updatable Tests

    [Test]
    public async Task MultiParam_Updatable_Should_Update_Target_With_Inlined_Helpers()
    {
        // Arrange
        var employee = new Employee
        {
            Id = 42,
            FirstName = "Multi",
            LastName = "Param",
            Email = "multi.param@test.com",
            IsActive = true,
            Department = new Department { Name = "R&D" }
        };

        var target = new EmployeeDto
        {
            Id = 0,
            FullName = "Old Name",
            Email = "old@email.com"
        };

        // Act
        var result = MultiParamUpdatableMapper.MapToDto(employee, target);

        // Assert — same reference, properties updated
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(42);
        await Assert.That(target.FullName).IsEqualTo("Multi Param");
        await Assert.That(target.Email).IsEqualTo("multi.param@test.com");
        await Assert.That(target.DepartmentName).IsEqualTo("R&D");
        await Assert.That(target.IsActive).IsTrue();
    }

    [Test]
    public async Task MultiParam_Updatable_Should_Handle_Null_Department()
    {
        // Arrange
        var employee = new Employee
        {
            Id = 99,
            FirstName = "No",
            LastName = "Dept",
            Email = "no.dept@test.com",
            IsActive = false,
            Department = null
        };

        var target = new EmployeeDto();

        // Act
        var result = MultiParamUpdatableMapper.MapToDto(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.FullName).IsEqualTo("No Dept");
        await Assert.That(target.DepartmentName).IsEqualTo("Unassigned");
    }

    [Test]
    public async Task MultiParam_Updatable_Should_Preserve_Unmapped_Properties()
    {
        // Arrange — target has properties not in the mapping
        var employee = new Employee
        {
            Id = 50,
            FirstName = "Preserve",
            LastName = "Test",
            Email = "preserve@test.com",
            IsActive = true,
            Department = new Department { Name = "Engineering" }
        };

        var target = new EmployeeDto
        {
            Salary = 120000m,
            AddressCount = 3,
            ProjectCount = 5,
            Skills = "C#, F#"
        };

        // Act
        var result = MultiParamUpdatableMapper.MapToDto(employee, target);

        // Assert — mapped properties updated
        await Assert.That(target.Id).IsEqualTo(50);
        await Assert.That(target.FullName).IsEqualTo("Preserve Test");

        // Unmapped properties preserved
        await Assert.That(target.Salary).IsEqualTo(120000m);
        await Assert.That(target.AddressCount).IsEqualTo(3);
        await Assert.That(target.ProjectCount).IsEqualTo(5);
        await Assert.That(target.Skills).IsEqualTo("C#, F#");
    }

    #endregion

    #region Multi-Param Expressive Method (method itself takes multiple params)

    [Test]
    public async Task MultiParam_Expressive_Method_Should_Generate_Correct_Expression()
    {
        // Arrange — MapWithYear takes (Employee, int) → generates Expression<Func<Employee, int, EmployeeDto>>
        var expression = MultiParamExpressiveMapper.MapWithYearExpression();

        // Act — compile and invoke (can't use directly with EF since it's a two-param expression)
        var compiled = expression.Compile();

        var employee = new Employee
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            IsActive = true,
            Department = new Department { Name = "Engineering" }
        };

        var result = compiled(employee, 2026);

        // Assert
        await Assert.That(result.Id).IsEqualTo(1);
        await Assert.That(result.FullName).IsEqualTo("John Doe");
        await Assert.That(result.Email).IsEqualTo("john@test.com");
        await Assert.That(result.DepartmentName).IsEqualTo("Engineering");
        await Assert.That(result.IsActive).IsTrue();
        await Assert.That(result.YearsOfExperience).IsEqualTo(6); // 2026 - 2020
    }

    [Test]
    public async Task MultiParam_Expressive_Method_Should_Have_Correct_Expression_Structure()
    {
        // Arrange
        var expression = MultiParamExpressiveMapper.MapWithYearExpression();
        var readable = expression.ToReadableString();

        // Assert — the expression should reference both parameters
        await Assert.That(readable).Contains("new EmployeeDto");
        await Assert.That(readable).Contains("FirstName");
        await Assert.That(readable).Contains("LastName");
        // The expression should have two lambda parameters
        await Assert.That(readable).Contains("currentYear");
    }

    #endregion

    #region Expression Tree Structure Validation

    [Test]
    public async Task MultiParam_Expression_Should_Inline_Helper_Into_Expression_Tree()
    {
        // Arrange
        var expression = MultiParamEmployeeMapper.MapToDtoExpression();
        var readable = expression.ToReadableString();

        // Assert — FormatName should be inlined, NOT appear as a method call
        await Assert.That(readable.Contains("FormatName")).IsFalse();
        // Instead, we should see the concatenation directly
        await Assert.That(readable).Contains("FirstName");
        await Assert.That(readable).Contains("LastName");
    }

    [Test]
    public async Task NamedArg_Expression_Should_Inline_Correctly()
    {
        // Arrange
        var expression = NamedArgEmployeeMapper.MapToDtoExpression();
        var readable = expression.ToReadableString();

        // Assert — FormatName should be inlined
        await Assert.That(readable.Contains("FormatName")).IsFalse();
        await Assert.That(readable).Contains("FirstName");
        await Assert.That(readable).Contains("LastName");
    }

    #endregion
}
