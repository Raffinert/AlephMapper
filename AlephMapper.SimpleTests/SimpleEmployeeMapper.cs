using AlephMapper;
using System.ComponentModel.DataAnnotations;

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class SimpleEmployeeMapper
{
    public static string GetEmail(Employee employee) =>
        employee.Email;

    // Null conditional operators
    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "No Department";

    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = GetEmail(employee),
        DepartmentName = GetDepartmentName(employee)
    };
}

public class EmployeeSimpleDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string DepartmentName { get; set; } = "";
}

public class Employee
{
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";

    [Required]
    public string Email { get; set; } = "";
    public Department? Department { get; set; }
}

public class Department
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public decimal? Budget { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public List<Employee> Employees { get; set; } = new();
}