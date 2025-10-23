namespace SampleApp.Entities;

public class Person
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public Address HomeAddress { get; set; } = new();
    public ICollection<PhoneNumber> ContactNumbers { get; set; } = new List<PhoneNumber>();
    public ICollection<Order> CustomerOrders { get; set; } = new List<Order>();
    
    // Computed properties for demonstration
    public string FullName => $"{FirstName} {LastName}";
    public int Age => DateTime.Now.Year - BirthDate.Year;
}

public class Address
{
    public int AddressId { get; set; }
    public string StreetAddress { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
}

public class PhoneNumber
{
    public int PhoneId { get; set; }
    public PhoneType PhoneType { get; set; }
    public string PhoneNumberValue { get; set; } = string.Empty;
}

public enum PhoneType
{
    Mobile,
    Home,
    Work,
    Fax
}

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public decimal Total { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public class OrderItem
{
    public int ItemId { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public decimal LineTotal { get; set; }
}