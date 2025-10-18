using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.ComprehensiveTests;

public class UpdateableTests
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
        // Add employee addresses for testing
        _context.EmployeeAddresses.AddRange(
            new EmployeeAddress { Id = 1, EmployeeId = 1, Street = "123 Main St", City = "Seattle", Country = "USA", Type = AddressType.Home, IsPrimary = true },
            new EmployeeAddress { Id = 2, EmployeeId = 2, Street = "789 Pine Rd", City = "Portland", Country = "USA", Type = AddressType.Home, IsPrimary = true }
        );

        await _context.SaveChangesAsync();
    }

    #region Simple Property Update Tests

    [Test]
    public async Task Simple_Property_Update_Should_Work()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstAsync(e => e.Id == 1);

        var target = new EmployeeSimpleDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeSimple(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(employee.Id);
        await Assert.That(target.FirstName).IsEqualTo(employee.FirstName);
        await Assert.That(target.LastName).IsEqualTo(employee.LastName);
        await Assert.That(target.Email).IsEqualTo(employee.Email);
        await Assert.That(target.DepartmentName).IsEqualTo("Engineering");
    }

    [Test]
    public async Task Simple_Update_Should_Handle_Nulls()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstAsync(e => e.Id == 4); // Alice Brown has no department

        var target = new EmployeeSimpleDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeSimple(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(4);
        await Assert.That(target.FirstName).IsEqualTo("Alice");
        await Assert.That(target.LastName).IsEqualTo("Brown");
        await Assert.That(target.DepartmentName).IsEqualTo("No Department");
    }

    #endregion

    #region Nested Object Update Tests

    [Test]
    public async Task Nested_Object_Update_Should_Create_New_Objects_When_Target_Is_Empty()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Profile)
            .ThenInclude(p => p!.ContactInfo)
            .FirstAsync(e => e.Id == 1); // John Doe with full profile

        var target = new EmployeeUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.FirstName).IsEqualTo("John");
        await Assert.That(target.LastName).IsEqualTo("Doe");
        await Assert.That(target.Email).IsEqualTo("john.doe@company.com");
        await Assert.That(target.IsActive).IsTrue();

        // Verify nested department object was created
        await Assert.That(target.Department).IsNotNull();
        await Assert.That(target.Department!.Id).IsEqualTo(1);
        await Assert.That(target.Department.Name).IsEqualTo("Engineering");
        await Assert.That(target.Department.IsActive).IsTrue();

        // Verify nested profile object was created
        await Assert.That(target.Profile).IsNotNull();
        await Assert.That(target.Profile!.Id).IsEqualTo(1);
        await Assert.That(target.Profile.Phone).IsEqualTo("+1-555-0101");
        await Assert.That(target.Profile.Skills).IsEqualTo("C#, .NET, SQL Server, Azure");
        await Assert.That(target.Profile.YearsOfExperience).IsEqualTo(8);

        // Verify nested contact info was created
        await Assert.That(target.Profile.ContactInfo).IsNotNull();
        await Assert.That(target.Profile.ContactInfo!.Id).IsEqualTo(1);
        await Assert.That(target.Profile.ContactInfo.EmergencyContactName).IsEqualTo("Mary Doe");
        await Assert.That(target.Profile.ContactInfo.EmergencyContactPhone).IsEqualTo("+1-555-0201");
    }

    [Test]
    public async Task Nested_Object_Update_Should_Update_Existing_Objects()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Profile)
            .ThenInclude(p => p!.ContactInfo)
            .FirstAsync(e => e.Id == 2); // Jane Smith

        // Pre-populate target with existing objects
        var target = new EmployeeUpdateDto
        {
            Id = 999, // Different ID to verify it gets updated
            FirstName = "Old Name",
            Department = new DepartmentUpdateDto
            {
                Id = 888,
                Name = "Old Department",
                Description = "Old Description"
            },
            Profile = new EmployeeProfileUpdateDto
            {
                Id = 777,
                Phone = "Old Phone",
                ContactInfo = new ContactInfoUpdateDto
                {
                    Id = 666,
                    EmergencyContactName = "Old Contact"
                }
            }
        };

        // Keep references to verify they're updated, not replaced
        var existingDepartment = target.Department;
        var existingProfile = target.Profile;
        var existingContactInfo = target.Profile.ContactInfo;

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert - Verify same instance is returned
        await Assert.That(result).IsSameReferenceAs(target);

        // Verify root properties are updated
        await Assert.That(target.Id).IsEqualTo(2); // Updated from Jane's data
        await Assert.That(target.FirstName).IsEqualTo("Jane");
        await Assert.That(target.LastName).IsEqualTo("Smith");

        // Verify existing nested objects are updated, not replaced
        await Assert.That(target.Department).IsSameReferenceAs(existingDepartment);
        await Assert.That(target.Profile).IsSameReferenceAs(existingProfile);
        await Assert.That(target.Profile.ContactInfo).IsSameReferenceAs(existingContactInfo);

        // Verify nested values are updated
        await Assert.That(target.Department.Id).IsEqualTo(1); // Updated from Jane's department
        await Assert.That(target.Department.Name).IsEqualTo("Engineering");

        await Assert.That(target.Profile.Id).IsEqualTo(2); // Updated from Jane's profile
        await Assert.That(target.Profile.Phone).IsEqualTo("+1-555-0102");

        await Assert.That(target.Profile.ContactInfo.Id).IsEqualTo(2); // Updated from Jane's contact info
        await Assert.That(target.Profile.ContactInfo.EmergencyContactName).IsEqualTo("Jim Smith");
    }

    [Test]
    public async Task Nested_Update_Should_Handle_Null_Navigation_Properties()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Profile)
            .ThenInclude(p => p!.ContactInfo)
            .FirstAsync(e => e.Id == 4); // Alice Brown has no department or profile

        var target = new EmployeeUpdateDto
        {
            Department = new DepartmentUpdateDto { Id = 999, Name = "Existing Department" },
            Profile = new EmployeeProfileUpdateDto { Id = 888, Phone = "Existing Phone" }
        };

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(4);
        await Assert.That(target.FirstName).IsEqualTo("Alice");
        await Assert.That(target.IsActive).IsFalse();

        // Verify null navigation properties result in null target properties
        await Assert.That(target.Department).IsNull();
        await Assert.That(target.Profile).IsNull();
    }

    [Test]
    public async Task Nested_Update_Should_Handle_Partial_Null_Chains()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Profile)
            .ThenInclude(p => p!.ContactInfo)
            .FirstAsync(e => e.Id == 6); // Diana Davis has no profile (therefore no ContactInfo)

        var target = new EmployeeUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Profile).IsNull(); // Diana has no profile at all
    }

    #endregion

    #region Combined Expressive and Updateable Tests

    [Test]
    public async Task Combined_Mapper_Expressive_Methods_Should_Work()
    {
        // Arrange
        var fullNameExpression = EmployeeCombinedMapper.GetFullNameExpression();
        var employeeDtoExpression = EmployeeCombinedMapper.MapToEmployeeDtoExpression();

        // Act
        var fullNames = await _context.Employees
            .Select(fullNameExpression)
            .ToListAsync();

        var employeeDtos = await _context.Employees
            .Include(e => e.Department)
            .Select(employeeDtoExpression)
            .ToListAsync();

        // Assert
        await Assert.That(fullNames.Count).IsEqualTo(6);
        await Assert.That(fullNames).Contains("John Doe");

        await Assert.That(employeeDtos.Count).IsEqualTo(6);
        var john = employeeDtos.First(e => e.Id == 1);
        await Assert.That(john.FullName).IsEqualTo("John Doe");
        await Assert.That(john.DepartmentName).IsEqualTo("Engineering");
    }

    [Test]
    public async Task Combined_Mapper_Updateable_Methods_Should_Work()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstAsync(e => e.Id == 1);

        var simpleTarget = new EmployeeSimpleDto();
        var updateTarget = new EmployeeUpdateDto();

        // Act
        var simpleResult = EmployeeCombinedMapper.UpdateEmployeeSimple(employee, simpleTarget);
        var updateResult = EmployeeCombinedMapper.UpdateEmployeeWithDepartment(employee, updateTarget);

        // Assert
        await Assert.That(simpleResult).IsSameReferenceAs(simpleTarget);
        await Assert.That(simpleTarget.DepartmentName).IsEqualTo("Engineering"); // Inlined method call

        await Assert.That(updateResult).IsSameReferenceAs(updateTarget);
        await Assert.That(updateTarget.Department).IsNotNull();
        await Assert.That(updateTarget.Department!.Name).IsEqualTo("Engineering");
    }

    #endregion

    #region Individual Component Update Tests

    [Test]
    public async Task Department_Update_Should_Work()
    {
        // Arrange
        var department = await _context.Departments.FirstAsync(d => d.Id == 1);
        var target = new DepartmentUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateDepartment(department, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.Name).IsEqualTo("Engineering");
        await Assert.That(target.Description).IsEqualTo("Software Development");
        await Assert.That(target.IsActive).IsTrue();
    }

    [Test]
    public async Task EmployeeProfile_Update_Should_Work()
    {
        // Arrange
        var profile = await _context.EmployeeProfiles
            .Include(p => p.ContactInfo)
            .FirstAsync(p => p.Id == 1);

        var target = new EmployeeProfileUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeProfile(profile, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.Phone).IsEqualTo("+1-555-0101");
        await Assert.That(target.Bio).IsEqualTo("Senior Software Engineer with 8 years experience");
        await Assert.That(target.Skills).IsEqualTo("C#, .NET, SQL Server, Azure");
        await Assert.That(target.YearsOfExperience).IsEqualTo(8);
        await Assert.That(target.ContactInfo).IsNotNull();
        await Assert.That(target.ContactInfo!.EmergencyContactName).IsEqualTo("Mary Doe");
    }

    [Test]
    public async Task ContactInfo_Update_Should_Work()
    {
        // Arrange
        var contactInfo = await _context.ContactInfos.FirstAsync(c => c.Id == 1);
        var target = new ContactInfoUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateContactInfo(contactInfo, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.EmergencyContactName).IsEqualTo("Mary Doe");
        await Assert.That(target.EmergencyContactPhone).IsEqualTo("+1-555-0201");
        await Assert.That(target.LinkedInUrl).IsEqualTo("https://linkedin.com/in/johndoe");
    }

    [Test]
    public async Task EmployeeAddress_Update_Should_Work()
    {
        // Arrange
        var address = await _context.EmployeeAddresses.FirstAsync(a => a.Id == 1);
        var target = new EmployeeAddressUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeAddress(address, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.Street).IsEqualTo("123 Main St");
        await Assert.That(target.City).IsEqualTo("Seattle");
        await Assert.That(target.Country).IsEqualTo("USA");
        await Assert.That(target.Type).IsEqualTo(AddressType.Home);
        await Assert.That(target.IsPrimary).IsTrue();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Update_Should_Handle_Empty_Target_Gracefully()
    {
        // Arrange
        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstAsync(e => e.Id == 1);

        var target = new EmployeeUpdateDto();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Id).IsEqualTo(1);
        await Assert.That(target.FirstName).IsEqualTo("John");
        await Assert.That(target.Department).IsNotNull();
    }

    [Test]
    public async Task Update_Should_Preserve_Collections_If_Present()
    {
        // Arrange
        var employee = await _context.Employees.FirstAsync(e => e.Id == 1);
        var target = new EmployeeUpdateDto
        {
            Addresses = new List<EmployeeAddressUpdateDto>
            {
                new() { Id = 999, Street = "Existing Address" }
            }
        };

        var existingAddresses = target.Addresses;

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployee(employee, target);

        // Assert
        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Addresses).IsSameReferenceAs(existingAddresses);
        await Assert.That(target.Addresses[0].Id).IsEqualTo(999); // Unchanged
    }

    #endregion
}