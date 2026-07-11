namespace SampleApp.Models;

public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public AddressDto Address { get; set; } = new();
    public List<PhoneDto> PhoneNumbers { get; set; } = new();
    public List<OrderDto> Orders { get; set; } = new();
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class PhoneDto
{
    public string Type { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
}

public class OrderDto
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class EmployeeSummaryDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public int Age { get; set; }
    public int YearsOfService { get; set; }
    public string Location { get; set; } = string.Empty;
    public string DepartmentTitle { get; set; } = string.Empty;
    public decimal TotalCompensation { get; set; }
}

public class ApplicantBriefDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ContactLine { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int YearsActive { get; set; }
    public string RoutingKey { get; set; } = string.Empty;
    public decimal Score { get; set; }
}

public class ContractorBriefDto
{
    // Same writable destination members used by ApplicantBriefDto, but a different explicit destination type.
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ContactLine { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int YearsActive { get; set; }
    public string RoutingKey { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string VendorLabel { get; set; } = string.Empty;
}