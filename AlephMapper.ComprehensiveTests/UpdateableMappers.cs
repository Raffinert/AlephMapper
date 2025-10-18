namespace AlephMapper.ComprehensiveTests;

// Mappers testing Updateable functionality

[Updateable]
public static partial class EmployeeUpdateMapper
{
    // Simple property update
    public static EmployeeSimpleDto UpdateEmployeeSimple(Employee employee) => new EmployeeSimpleDto
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = employee.Department?.Name ?? "No Department"
    };

    // Nested object update
    public static EmployeeUpdateDto UpdateEmployee(Employee employee) => new EmployeeUpdateDto
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        IsActive = employee.IsActive,
        Department = employee.Department != null ? UpdateDepartment(employee.Department) : null,
        Profile = employee.Profile != null ? UpdateEmployeeProfile(employee.Profile) : null
    };

    public static DepartmentUpdateDto UpdateDepartment(Department department) => new DepartmentUpdateDto
    {
        Id = department.Id,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive
    };

    public static EmployeeProfileUpdateDto UpdateEmployeeProfile(EmployeeProfile profile) => new EmployeeProfileUpdateDto
    {
        Id = profile.Id,
        Phone = profile.Phone,
        Bio = profile.Bio,
        Skills = profile.Skills,
        YearsOfExperience = profile.YearsOfExperience,
        ContactInfo = profile.ContactInfo != null ? UpdateContactInfo(profile.ContactInfo) : null
    };

    public static ContactInfoUpdateDto UpdateContactInfo(ContactInfo contactInfo) => new ContactInfoUpdateDto
    {
        Id = contactInfo.Id,
        EmergencyContactName = contactInfo.EmergencyContactName,
        EmergencyContactPhone = contactInfo.EmergencyContactPhone,
        LinkedInUrl = contactInfo.LinkedInUrl
    };

    // Collection update (simplified)
    public static EmployeeAddressUpdateDto UpdateEmployeeAddress(EmployeeAddress address) => new EmployeeAddressUpdateDto
    {
        Id = address.Id,
        Street = address.Street,
        City = address.City,
        State = address.State,
        Country = address.Country,
        ZipCode = address.ZipCode,
        Type = address.Type,
        IsPrimary = address.IsPrimary
    };
}

// Test both Expressive and Updateable on the same class
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class EmployeeCombinedMapper
{
    // Expressive methods
    public static string GetFullName(Employee employee) => 
        $"{employee.FirstName} {employee.LastName}";

    public static string GetDepartmentName(Employee employee) => 
        employee.Department?.Name ?? "No Department";

    public static EmployeeDto MapToEmployeeDto(Employee employee) => new EmployeeDto
    {
        Id = employee.Id,
        FullName = GetFullName(employee),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive
    };

    // Updateable methods
    [Updateable]
    public static EmployeeSimpleDto UpdateEmployeeSimple(Employee employee) => new EmployeeSimpleDto
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee) // This should get inlined
    };

    [Updateable]
    public static EmployeeUpdateDto UpdateEmployeeWithDepartment(Employee employee) => new EmployeeUpdateDto
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        IsActive = employee.IsActive,
        Department = employee.Department != null ? new DepartmentUpdateDto
        {
            Id = employee.Department.Id,
            Name = employee.Department.Name,
            Description = employee.Department.Description,
            IsActive = employee.Department.IsActive
        } : null
    };
}