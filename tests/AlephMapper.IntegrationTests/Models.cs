using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.ComprehensiveTests;

// Core domain models for comprehensive testing
public class Employee
{
    public int Id { get; set; }
    
    [Required]
    public string FirstName { get; set; } = "";
    
    [Required]
    public string LastName { get; set; } = "";
    
    [Required]
    public string Email { get; set; } = "";
    
    public DateTime? BirthDate { get; set; }
    
    public decimal? Salary { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public List<Employee> Subordinates { get; set; } = new();
    
    public EmployeeProfile? Profile { get; set; }
    public List<EmployeeAddress> Addresses { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<EmployeeProject> EmployeeProjects { get; set; } = new();
    public List<Timesheet> Timesheets { get; set; } = new();
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

public class EmployeeProfile
{
    public int Id { get; set; }
    
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? Skills { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? ProfilePictureUrl { get; set; }
    
    // Navigation properties
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public ContactInfo? ContactInfo { get; set; }
}

public class ContactInfo
{
    public int Id { get; set; }
    
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }
    
    // Navigation properties
    public int EmployeeProfileId { get; set; }
    public EmployeeProfile EmployeeProfile { get; set; } = null!;
}

public class EmployeeAddress
{
    public int Id { get; set; }
    
    [Required]
    public string Street { get; set; } = "";
    
    [Required]
    public string City { get; set; } = "";
    
    public string? State { get; set; }
    
    [Required]
    public string Country { get; set; } = "";
    
    public string? ZipCode { get; set; }
    
    public AddressType Type { get; set; } = AddressType.Home;
    
    public bool IsPrimary { get; set; }
    
    // Navigation properties
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}

public class Project
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = "";
    
    public string? Description { get; set; }
    
    public DateTime StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public decimal? Budget { get; set; }
    
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    
    // Navigation properties
    public List<Employee> Employees { get; set; } = new();
    public List<EmployeeProject> EmployeeProjects { get; set; } = new();
    public List<Timesheet> Timesheets { get; set; } = new();
}

public class EmployeeProject
{
    public int Id { get; set; }
    
    public DateTime AssignedDate { get; set; }
    
    public DateTime? UnassignedDate { get; set; }
    
    public ProjectRole Role { get; set; } = ProjectRole.Developer;
    
    public decimal? HourlyRate { get; set; }
    
    // Navigation properties
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
}

public class Timesheet
{
    public int Id { get; set; }
    
    public DateTime Date { get; set; }
    
    public decimal HoursWorked { get; set; }
    
    public string? Description { get; set; }
    
    public bool IsApproved { get; set; }
    
    // Navigation properties
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
}

// Enums
public enum AddressType
{
    Home,
    Work,
    Billing,
    Shipping
}

public enum ProjectStatus
{
    Planning,
    InProgress,
    OnHold,
    Completed,
    Cancelled
}

public enum ProjectRole
{
    Developer,
    TechnicalLead,
    ProjectManager,
    Architect,
    QA,
    DevOps
}

// DTO classes for mapping tests
public class EmployeeDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public int? Age { get; set; }
    public decimal? Salary { get; set; }
    public bool IsActive { get; set; }
    public string DepartmentName { get; set; } = "";
    public string? ManagerName { get; set; }
    public bool HasProfile { get; set; }
    public int AddressCount { get; set; }
    public int ProjectCount { get; set; }
    public string PrimaryAddress { get; set; } = "";
    public string Skills { get; set; } = "";
    public int YearsOfExperience { get; set; }
    public bool HasEmergencyContact { get; set; }
}

public class EmployeeSimpleDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string DepartmentName { get; set; } = "";
}

public class EmployeeProfileDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? Skills { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? Budget { get; set; }
    public bool IsActive { get; set; }
    public string? ManagerName { get; set; }
    public int EmployeeCount { get; set; }
    public decimal? AverageSalary { get; set; }
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public ProjectStatus Status { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalHours { get; set; }
    public bool IsActive { get; set; }
}

// Complex nested DTOs for update testing
public class EmployeeUpdateDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public DepartmentUpdateDto? Department { get; set; }
    public EmployeeProfileUpdateDto? Profile { get; set; }
    public List<EmployeeAddressUpdateDto> Addresses { get; set; } = new();
}

public class DepartmentUpdateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeProfileUpdateDto
{
    public int Id { get; set; }
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? Skills { get; set; }
    public int? YearsOfExperience { get; set; }
    public ContactInfoUpdateDto? ContactInfo { get; set; }
}

public class ContactInfoUpdateDto
{
    public int Id { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? LinkedInUrl { get; set; }
}

public class EmployeeAddressUpdateDto
{
    public int Id { get; set; }
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string? State { get; set; }
    public string Country { get; set; } = "";
    public string? ZipCode { get; set; }
    public AddressType Type { get; set; }
    public bool IsPrimary { get; set; }
}