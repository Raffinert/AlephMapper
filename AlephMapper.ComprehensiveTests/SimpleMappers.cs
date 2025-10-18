namespace AlephMapper.ComprehensiveTests;

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class SimpleEmployeeMapper
{
    // Basic property mapping
    public static string GetFullName(Employee employee) => 
        $"{employee.FirstName} {employee.LastName}";

    public static string GetEmail(Employee employee) => 
        employee.Email;

    // Null conditional operators
    public static string GetDepartmentName(Employee employee) => 
        employee.Department?.Name ?? "No Department";

    public static string GetManagerName(Employee employee) => 
        employee.Manager?.FirstName ?? "No Manager";

    public static string GetPhone(Employee employee) => 
        employee.Profile?.Phone ?? "No Phone";

    // Simple boolean expressions
    public static bool HasProfile(Employee employee) => 
        employee.Profile != null;

    public static bool IsActive(Employee employee) => 
        employee.IsActive;

    // Collection count
    public static int GetAddressCount(Employee employee) => 
        employee.Addresses.Count;

    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = GetEmail(employee),
        DepartmentName = GetDepartmentName(employee)
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class SimpleIgnoreMapper
{
    public static string GetFullName(Employee employee) => 
        $"{employee.FirstName} {employee.LastName}";

    public static string GetDepartmentName(Employee employee) => 
        employee.Department?.Name ?? "No Department";

    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee)
    };
}

[Updateable]
public static partial class SimpleUpdateMapper
{
    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = employee.Department?.Name ?? "No Department"
    };

    public static DepartmentUpdateDto MapToDepartmentDto(Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive
    };
}