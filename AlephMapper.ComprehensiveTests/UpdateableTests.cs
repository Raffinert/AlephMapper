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
        // Arrange - DTO as source
        var sourceDto = new EmployeeSimpleDto
        {
            Id = 100,
            FirstName = "Updated John",
            LastName = "Updated Doe",
            Email = "updated.john@company.com",
            DepartmentName = "Updated Engineering" // This won't be mapped as it's computed
        };

        var targetEmployee = new Employee
        {
            Id = 1,
            FirstName = "Original John",
            LastName = "Original Doe",
            Email = "original.john@company.com",
            IsActive = true
        };

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromSimpleDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(100); // Updated from DTO
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Updated John");
        await Assert.That(targetEmployee.LastName).IsEqualTo("Updated Doe");
        await Assert.That(targetEmployee.Email).IsEqualTo("updated.john@company.com");
        await Assert.That(targetEmployee.IsActive).IsTrue(); // Unchanged as not in simple DTO
    }

    [Test]
    public async Task Simple_Update_Should_Handle_Partial_Updates()
    {
        // Arrange - DTO with some properties
        var sourceDto = new EmployeeSimpleDto
        {
            Id = 200,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@company.com"
        };

        // Target with additional properties that should be preserved
        var targetEmployee = new Employee
        {
            Id = 1,
            FirstName = "Old Jane",
            LastName = "Old Smith",
            Email = "old.jane@company.com",
            IsActive = false,
            Salary = 75000m,
            BirthDate = new DateTime(1990, 5, 15)
        };

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromSimpleDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(200);
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Jane");
        await Assert.That(targetEmployee.LastName).IsEqualTo("Smith");
        await Assert.That(targetEmployee.Email).IsEqualTo("jane.smith@company.com");
        // These should be preserved since they're not in the simple DTO
        await Assert.That(targetEmployee.IsActive).IsFalse();
        await Assert.That(targetEmployee.Salary).IsEqualTo(75000m);
        await Assert.That(targetEmployee.BirthDate).IsEqualTo(new DateTime(1990, 5, 15));
    }

    #endregion

    #region Nested Object Update Tests

    [Test]
    public async Task Nested_Object_Update_Should_Create_New_Objects_When_Target_Is_Empty()
    {
        // Arrange - DTO with nested objects as source
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 300,
            FirstName = "Complex John",
            LastName = "Complex Doe",
            Email = "complex.john@company.com",
            IsActive = true,
            Department = new DepartmentUpdateDto
            {
                Id = 10,
                Name = "New Engineering",
                Description = "Advanced Software Development",
                IsActive = true
            },
            Profile = new EmployeeProfileUpdateDto
            {
                Id = 20,
                Phone = "+1-555-9999",
                Skills = "Advanced C#, .NET, Cloud Architecture",
                YearsOfExperience = 12,
                ContactInfo = new ContactInfoUpdateDto
                {
                    Id = 30,
                    EmergencyContactName = "Updated Mary Doe",
                    EmergencyContactPhone = "+1-555-8888",
                    LinkedInUrl = "https://linkedin.com/in/complexjohndoe"
                }
            }
        };

        var targetEmployee = new Employee();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(300);
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Complex John");
        await Assert.That(targetEmployee.LastName).IsEqualTo("Complex Doe");
        await Assert.That(targetEmployee.Email).IsEqualTo("complex.john@company.com");
        await Assert.That(targetEmployee.IsActive).IsTrue();

        // Verify nested department object was created
        await Assert.That(targetEmployee.Department).IsNotNull();
        await Assert.That(targetEmployee.Department!.Id).IsEqualTo(10);
        await Assert.That(targetEmployee.Department.Name).IsEqualTo("New Engineering");
        await Assert.That(targetEmployee.Department.Description).IsEqualTo("Advanced Software Development");
        await Assert.That(targetEmployee.Department.IsActive).IsTrue();

        // Verify nested profile object was created
        await Assert.That(targetEmployee.Profile).IsNotNull();
        await Assert.That(targetEmployee.Profile!.Id).IsEqualTo(20);
        await Assert.That(targetEmployee.Profile.Phone).IsEqualTo("+1-555-9999");
        await Assert.That(targetEmployee.Profile.Skills).IsEqualTo("Advanced C#, .NET, Cloud Architecture");
        await Assert.That(targetEmployee.Profile.YearsOfExperience).IsEqualTo(12);

        // Verify nested contact info was created
        await Assert.That(targetEmployee.Profile.ContactInfo).IsNotNull();
        await Assert.That(targetEmployee.Profile.ContactInfo!.Id).IsEqualTo(30);
        await Assert.That(targetEmployee.Profile.ContactInfo.EmergencyContactName).IsEqualTo("Updated Mary Doe");
        await Assert.That(targetEmployee.Profile.ContactInfo.EmergencyContactPhone).IsEqualTo("+1-555-8888");
        await Assert.That(targetEmployee.Profile.ContactInfo.LinkedInUrl).IsEqualTo("https://linkedin.com/in/complexjohndoe");
    }

    [Test]
    public async Task Nested_Object_Update_Should_Update_Existing_Objects()
    {
        // Arrange - DTO with updated values
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 400,
            FirstName = "Updated Jane",
            LastName = "Updated Smith",
            Email = "updated.jane@company.com",
            IsActive = false,
            Department = new DepartmentUpdateDto
            {
                Id = 11,
                Name = "Updated Marketing",
                Description = "Updated Digital Marketing",
                IsActive = false
            },
            Profile = new EmployeeProfileUpdateDto
            {
                Id = 21,
                Phone = "+1-555-7777",
                Skills = "Updated Marketing Skills",
                YearsOfExperience = 5,
                ContactInfo = new ContactInfoUpdateDto
                {
                    Id = 31,
                    EmergencyContactName = "Updated Emergency Contact",
                    EmergencyContactPhone = "+1-555-6666"
                }
            }
        };

        // Pre-populate target with existing objects
        var targetEmployee = new Employee
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@company.com",
            IsActive = true,
            Department = new Department
            {
                Id = 2,
                Name = "Marketing",
                Description = "Digital Marketing",
                IsActive = true
            },
            Profile = new EmployeeProfile
            {
                Id = 2,
                Phone = "+1-555-0102",
                Skills = "Marketing, Social Media",
                YearsOfExperience = 3,
                ContactInfo = new ContactInfo
                {
                    Id = 2,
                    EmergencyContactName = "Jim Smith",
                    EmergencyContactPhone = "+1-555-0202"
                }
            }
        };

        // Keep references to verify they're updated, not replaced
        var existingDepartment = targetEmployee.Department;
        var existingProfile = targetEmployee.Profile;
        var existingContactInfo = targetEmployee.Profile.ContactInfo;

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert - Verify same instance is returned
        await Assert.That(result).IsSameReferenceAs(targetEmployee);

        // Verify root properties are updated
        await Assert.That(targetEmployee.Id).IsEqualTo(400);
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Updated Jane");
        await Assert.That(targetEmployee.LastName).IsEqualTo("Updated Smith");
        await Assert.That(targetEmployee.Email).IsEqualTo("updated.jane@company.com");
        await Assert.That(targetEmployee.IsActive).IsFalse();

        // Verify existing nested objects are updated, not replaced
        await Assert.That(targetEmployee.Department).IsSameReferenceAs(existingDepartment);
        await Assert.That(targetEmployee.Profile).IsSameReferenceAs(existingProfile);
        await Assert.That(targetEmployee.Profile.ContactInfo).IsSameReferenceAs(existingContactInfo);

        // Verify nested values are updated
        await Assert.That(targetEmployee.Department.Id).IsEqualTo(11);
        await Assert.That(targetEmployee.Department.Name).IsEqualTo("Updated Marketing");
        await Assert.That(targetEmployee.Department.Description).IsEqualTo("Updated Digital Marketing");
        await Assert.That(targetEmployee.Department.IsActive).IsFalse();

        await Assert.That(targetEmployee.Profile.Id).IsEqualTo(21);
        await Assert.That(targetEmployee.Profile.Phone).IsEqualTo("+1-555-7777");
        await Assert.That(targetEmployee.Profile.Skills).IsEqualTo("Updated Marketing Skills");
        await Assert.That(targetEmployee.Profile.YearsOfExperience).IsEqualTo(5);

        await Assert.That(targetEmployee.Profile.ContactInfo.Id).IsEqualTo(31);
        await Assert.That(targetEmployee.Profile.ContactInfo.EmergencyContactName).IsEqualTo("Updated Emergency Contact");
        await Assert.That(targetEmployee.Profile.ContactInfo.EmergencyContactPhone).IsEqualTo("+1-555-6666");
    }

    [Test]
    public async Task Nested_Update_Should_Handle_Null_Navigation_Properties()
    {
        // Arrange - DTO with null nested objects
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 500,
            FirstName = "Null Test",
            LastName = "Employee",
            Email = "null.test@company.com",
            IsActive = true,
            Department = null, // Null department
            Profile = null     // Null profile
        };

        var targetEmployee = new Employee
        {
            Id = 1,
            FirstName = "Original",
            LastName = "Employee",
            Email = "original@company.com",
            IsActive = false,
            Department = new Department { Id = 1, Name = "Existing Department" },
            Profile = new EmployeeProfile { Id = 1, Phone = "Existing Phone" }
        };

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(500);
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Null Test");
        await Assert.That(targetEmployee.LastName).IsEqualTo("Employee");
        await Assert.That(targetEmployee.Email).IsEqualTo("null.test@company.com");
        await Assert.That(targetEmployee.IsActive).IsTrue();

        // Verify null navigation properties result in null target properties
        await Assert.That(targetEmployee.Department).IsNull();
        await Assert.That(targetEmployee.Profile).IsNull();
    }

    [Test]
    public async Task Nested_Update_Should_Handle_Partial_Null_Chains()
    {
        // Arrange - DTO with department but no profile
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 600,
            FirstName = "Partial",
            LastName = "Update",
            Email = "partial@company.com",
            IsActive = true,
            Department = new DepartmentUpdateDto
            {
                Id = 12,
                Name = "Partial Department",
                IsActive = true
            },
            Profile = null // No profile
        };

        var targetEmployee = new Employee();

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(600);
        await Assert.That(targetEmployee.Department).IsNotNull();
        await Assert.That(targetEmployee.Department!.Name).IsEqualTo("Partial Department");
        await Assert.That(targetEmployee.Profile).IsNull(); // Should remain null
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
        // Arrange - DTOs as source
        var simpleDto = new EmployeeSimpleDto
        {
            Id = 700,
            FirstName = "Combined Simple",
            LastName = "Test",
            Email = "combined.simple@test.com"
        };

        var updateDto = new EmployeeUpdateDto
        {
            Id = 800,
            FirstName = "Combined Update",
            LastName = "Test",
            Email = "combined.update@test.com",
            IsActive = false,
            Department = new DepartmentUpdateDto
            {
                Id = 13,
                Name = "Combined Department",
                IsActive = false
            }
        };

        var simpleTarget = new Employee();
        var updateTarget = new Employee();

        // Act
        var simpleResult = EmployeeCombinedMapper.UpdateEmployeeFromSimpleDto(simpleDto, simpleTarget);
        var updateResult = EmployeeCombinedMapper.UpdateEmployeeFromDto(updateDto, updateTarget);

        // Assert
        await Assert.That(simpleResult).IsSameReferenceAs(simpleTarget);
        await Assert.That(simpleTarget.Id).IsEqualTo(700);
        await Assert.That(simpleTarget.FirstName).IsEqualTo("Combined Simple");

        await Assert.That(updateResult).IsSameReferenceAs(updateTarget);
        await Assert.That(updateTarget.Id).IsEqualTo(800);
        await Assert.That(updateTarget.Department).IsNotNull();
        await Assert.That(updateTarget.Department!.Name).IsEqualTo("Combined Department");
        await Assert.That(updateTarget.Department.IsActive).IsFalse();
    }

    #endregion

    #region Conditional Expression Tests

    [Test]
    public async Task Conditional_Expression_Update_Should_Work_With_Null_Check()
    {
        // Arrange - Test the source == null ? null : new TargetType pattern
        var sourceEmployee = new Employee
        {
            Id = 999,
            FirstName = "Conditional",
            LastName = "Test",
            Email = "conditional@test.com",
            Department = new Department { Name = "Test Department" }
        };

        var targetDto = new EmployeeSimpleDto
        {
            Id = 1,
            FirstName = "Old",
            LastName = "Values",
            Email = "old@email.com"
        };

        // Act
        var result = ConditionalUpdateMapper.ConditionalMapping(sourceEmployee, targetDto);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetDto);
        await Assert.That(targetDto.Id).IsEqualTo(999);
        await Assert.That(targetDto.FirstName).IsEqualTo("Conditional");
        await Assert.That(targetDto.LastName).IsEqualTo("Test");
        await Assert.That(targetDto.Email).IsEqualTo("conditional@test.com");
        await Assert.That(targetDto.DepartmentName).IsEqualTo("Test Department");
    }

    [Test]
    public async Task Conditional_Expression_Update_Should_Handle_Null_Source()
    {
        // Arrange - Test with null source
        Employee? sourceEmployee = null;
        var targetDto = new EmployeeSimpleDto
        {
            Id = 1,
            FirstName = "Should",
            LastName = "Remain",
            Email = "unchanged@email.com"
        };

        // Act
        var result = ConditionalUpdateMapper.ConditionalMapping(sourceEmployee, targetDto);

        // Assert - When source is null, target should remain unchanged
        await Assert.That(result).IsSameReferenceAs(targetDto);
        await Assert.That(targetDto.Id).IsEqualTo(1);
        await Assert.That(targetDto.FirstName).IsEqualTo("Should");
        await Assert.That(targetDto.LastName).IsEqualTo("Remain");
        await Assert.That(targetDto.Email).IsEqualTo("unchanged@email.com");
    }

    [Test]
    public async Task Conditional_Expression_Update_Should_Work_With_Inverted_Condition()
    {
        // Arrange - Test the source != null ? new TargetType : null pattern
        var sourceDepartment = new Department
        {
            Id = 888,
            Name = "Inverted Test",
            Description = "Testing inverted conditional",
            IsActive = false
        };

        var targetDto = new DepartmentUpdateDto
        {
            Id = 1,
            Name = "Old Name",
            IsActive = true
        };

        // Act
        var result = ConditionalUpdateMapper.ConditionalDepartmentMapping(sourceDepartment, targetDto);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetDto);
        await Assert.That(targetDto.Id).IsEqualTo(888);
        await Assert.That(targetDto.Name).IsEqualTo("Inverted Test");
        await Assert.That(targetDto.Description).IsEqualTo("Testing inverted conditional");
        await Assert.That(targetDto.IsActive).IsFalse();
    }

    [Test]
    public async Task Conditional_Expression_Update_Should_Handle_Null_Source_Inverted()
    {
        // Arrange - Test inverted conditional with null source
        Department? sourceDepartment = null;
        var targetDto = new DepartmentUpdateDto
        {
            Id = 1,
            Name = "Should Remain",
            IsActive = true
        };

        // Act
        var result = ConditionalUpdateMapper.ConditionalDepartmentMapping(sourceDepartment, targetDto);

        // Assert - When source is null, target should remain unchanged
        await Assert.That(result).IsSameReferenceAs(targetDto);
        await Assert.That(targetDto.Id).IsEqualTo(1);
        await Assert.That(targetDto.Name).IsEqualTo("Should Remain");
        await Assert.That(targetDto.IsActive).IsTrue();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Update_Should_Handle_Empty_Target_Gracefully()
    {
        // Arrange - DTO as source with fully populated data
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 5000,
            FirstName = "Edge Case",
            LastName = "Employee",
            Email = "edgecase@company.com",
            IsActive = true,
            Department = new DepartmentUpdateDto
            {
                Id = 14,
                Name = "Edge Department",
                IsActive = true
            }
        };

        var targetEmployee = new Employee(); // Empty target

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(5000);
        await Assert.That(targetEmployee.FirstName).IsEqualTo("Edge Case");
        await Assert.That(targetEmployee.Department).IsNotNull();
        await Assert.That(targetEmployee.Department!.Name).IsEqualTo("Edge Department");
    }

    [Test]
    public async Task Update_Should_Preserve_Collections_If_Present()
    {
        // Arrange - DTO without addresses
        var sourceDto = new EmployeeUpdateDto
        {
            Id = 6000,
            FirstName = "Collection",
            LastName = "Test",
            Email = "collection@test.com",
            IsActive = true
        };

        var targetEmployee = new Employee
        {
            Addresses = new List<EmployeeAddress> 
            { 
                new EmployeeAddress { Id = 999, Street = "Existing Address" }
            },
            Projects = new List<Project>
            {
                new Project { Id = 888, Name = "Existing Project" }
            }
        };

        var existingAddresses = targetEmployee.Addresses;
        var existingProjects = targetEmployee.Projects;

        // Act
        var result = EmployeeUpdateMapper.UpdateEmployeeFromDto(sourceDto, targetEmployee);

        // Assert
        await Assert.That(result).IsSameReferenceAs(targetEmployee);
        await Assert.That(targetEmployee.Id).IsEqualTo(6000);
        
        // Collections should be preserved since they're not part of the DTO mapping
        await Assert.That(targetEmployee.Addresses).IsSameReferenceAs(existingAddresses);
        await Assert.That(targetEmployee.Projects).IsSameReferenceAs(existingProjects);
        await Assert.That(targetEmployee.Addresses[0].Id).IsEqualTo(999); // Unchanged
        await Assert.That(targetEmployee.Projects[0].Id).IsEqualTo(888); // Unchanged
    }

    #endregion
}