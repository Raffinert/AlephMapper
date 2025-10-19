namespace AlephMapper.ComprehensiveTests;

// Add a test mapper that uses conditional expressions like the PersonMapper
[Updatable]
public static partial class ConditionalUpdateMapper
{
    // This method mimics the PersonMapper pattern: source == null ? null : new TargetType { ... }
    public static EmployeeSimpleDto ConditionalMapping(Employee? employee) =>
        employee == null ? null : new EmployeeSimpleDto
        {
            Id = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            DepartmentName = employee.Department?.Name ?? "No Department"
        };

    // Test with inverted conditional: source != null ? new TargetType { ... } : null  
    public static DepartmentUpdateDto ConditionalDepartmentMapping(Department? department) =>
        department != null ? new DepartmentUpdateDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive
        } : null;
}

// Mappers testing Updatable functionality

[Updatable]
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

    // REVERSE MAPPING METHODS - DTO to Entity
    
    // Simple DTO to Employee update
    public static Employee UpdateEmployeeFromSimpleDto(EmployeeSimpleDto dto) => new Employee
    {
        Id = dto.Id,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email
        // Note: DepartmentName is not mapped back as it's a computed property
    };

    // Complex DTO to Employee update
    public static Employee UpdateEmployeeFromDto(EmployeeUpdateDto dto) => new Employee
    {
        Id = dto.Id,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email,
        IsActive = dto.IsActive,
        Department = dto.Department != null ? UpdateDepartmentFromDto(dto.Department) : null,
        Profile = dto.Profile != null ? UpdateEmployeeProfileFromDto(dto.Profile) : null
    };

    public static Department UpdateDepartmentFromDto(DepartmentUpdateDto dto) => new Department
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive
    };

    public static EmployeeProfile UpdateEmployeeProfileFromDto(EmployeeProfileUpdateDto dto) => new EmployeeProfile
    {
        Id = dto.Id,
        Phone = dto.Phone,
        Bio = dto.Bio,
        Skills = dto.Skills,
        YearsOfExperience = dto.YearsOfExperience,
        ContactInfo = dto.ContactInfo != null ? UpdateContactInfoFromDto(dto.ContactInfo) : null
    };

    public static ContactInfo UpdateContactInfoFromDto(ContactInfoUpdateDto dto) => new ContactInfo
    {
        Id = dto.Id,
        EmergencyContactName = dto.EmergencyContactName,
        EmergencyContactPhone = dto.EmergencyContactPhone,
        LinkedInUrl = dto.LinkedInUrl
    };

    public static EmployeeAddress UpdateEmployeeAddressFromDto(EmployeeAddressUpdateDto dto) => new EmployeeAddress
    {
        Id = dto.Id,
        Street = dto.Street,
        City = dto.City,
        State = dto.State,
        Country = dto.Country,
        ZipCode = dto.ZipCode,
        Type = dto.Type,
        IsPrimary = dto.IsPrimary
    };
}

// Test both Expressive and Updatable on the same class
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

    // Updatable methods
    [Updatable]
    public static EmployeeSimpleDto UpdateEmployeeSimple(Employee employee) => new EmployeeSimpleDto
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee) // This should get inlined
    };

    [Updatable]
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

    // Reverse mapping methods for combined testing
    [Updatable]
    public static Employee UpdateEmployeeFromSimpleDto(EmployeeSimpleDto dto) => new Employee
    {
        Id = dto.Id,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email
    };

    [Updatable]
    public static Employee UpdateEmployeeFromDto(EmployeeUpdateDto dto) => new Employee
    {
        Id = dto.Id,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email,
        IsActive = dto.IsActive,
        Department = dto.Department != null ? new Department
        {
            Id = dto.Department.Id,
            Name = dto.Department.Name,
            Description = dto.Department.Description,
            IsActive = dto.Department.IsActive
        } : null
    };
}