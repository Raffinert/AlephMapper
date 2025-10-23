using SampleApp.Entities;
using SampleApp.Mappers;
using SampleApp.Services;

// Create sample entity data
var person = new Person
{
    PersonId = 1,
    FirstName = "John",
    LastName = "Doe",
    EmailAddress = "john.doe@email.com",
    BirthDate = new DateTime(1990, 5, 15),
    HomeAddress = new Address
    {
        AddressId = 1,
        StreetAddress = "123 Main St",
        CityName = "New York",
        StateName = "NY",
        PostalCode = "10001",
        CountryName = "USA"
    },
    ContactNumbers = new List<PhoneNumber>
    {
        new() { PhoneId = 1, PhoneType = PhoneType.Mobile, PhoneNumberValue = "5551234567" },
        new() { PhoneId = 2, PhoneType = PhoneType.Home, PhoneNumberValue = "5559876543" }
    },
    CustomerOrders = new List<Order>
    {
        new()
        {
            Id = 1001,
            CreatedDate = DateTime.Now.AddDays(-30),
            Total = 299.99m,
            OrderStatus = OrderStatus.Delivered,
            OrderItems = new List<OrderItem>
            {
                new() { ItemId = 1, ProductId = 101, Name = "Laptop", Qty = 1, Price = 999.99m, LineTotal = 999.99m },
                new() { ItemId = 2, ProductId = 102, Name = "Mouse", Qty = 2, Price = 25.00m, LineTotal = 50.00m }
            }
        },
        new()
        {
            Id = 1002,
            CreatedDate = DateTime.Now.AddDays(-10),
            Total = 159.99m,
            OrderStatus = OrderStatus.Shipped,
            OrderItems = new List<OrderItem>
            {
                new() { ItemId = 3, ProductId = 103, Name = "Keyboard", Qty = 1, Price = 79.99m, LineTotal = 79.99m },
                new() { ItemId = 4, ProductId = 104, Name = "Monitor Stand", Qty = 1, Price = 79.99m, LineTotal = 79.99m }
            }
        }
    }
};

Console.WriteLine("=== Complex Manual Mapping Demonstration ===\n");

// Demonstrate basic mapping with expression-bodied methods
Console.WriteLine("1. Basic Entity to DTO Mapping:");
var personDto = PersonMapper.ToDto(person);
var expression = PersonMapper.ToDtoExpression();
PersonMapper.ToDto(person, personDto);
Console.WriteLine($"   Person: {personDto.FirstName} {personDto.LastName}");
Console.WriteLine($"   Email: {personDto.Email}");
Console.WriteLine($"   Address: {AddressMapper.GetFormattedAddress(personDto.Address)}");
Console.WriteLine($"   Phone Numbers: {string.Join(", ", personDto.PhoneNumbers.Select(p => $"{p.Type}: {p.Number}"))}");
Console.WriteLine($"   Orders Count: {personDto.Orders.Count}");

// Demonstrate reverse mapping
Console.WriteLine("\n2. DTO to Entity Reverse Mapping:");
var mappedBackEntity = PersonMapper.ToEntity(personDto);
PersonMapper.ToEntity(personDto, mappedBackEntity);
Console.WriteLine($"   Mapped Back - Full Name: {mappedBackEntity.FullName}");
Console.WriteLine($"   Age: {mappedBackEntity.Age}");

// Demonstrate phone number formatting and validation
Console.WriteLine("\n3. Phone Number Processing:");
foreach (var phone in person.ContactNumbers)
{
    var phoneDto = PhoneMapper.ToDto(phone);
    Console.WriteLine($"   {PhoneMapper.GetPhoneTypeString(phone.PhoneType)}: {phoneDto.Number}");
    Console.WriteLine($"   Valid: {PhoneMapper.IsValidPhoneDto(phoneDto)}");
    Console.WriteLine($"   Clean Number: {PhoneMapper.CleanPhoneNumber(phoneDto.Number)}");
}

// Demonstrate order processing and calculations
Console.WriteLine("\n4. Order Processing and Calculations:");
foreach (var order in personDto.Orders)
{
    Console.WriteLine($"   Order #{order.OrderId} - Status: {order.Status}");
    Console.WriteLine($"   Order Date: {order.OrderDate:yyyy-MM-dd}");
    Console.WriteLine($"   Total Amount: {order.TotalAmount:C}");
    Console.WriteLine($"   Items Count: {OrderMapper.GetTotalItemCount(order)}");
    Console.WriteLine($"   Calculated Total: {OrderMapper.CalculateTotalValue(order):C}");
    Console.WriteLine($"   Valid Order: {OrderMapper.IsValidOrder(order)}");

    foreach (var item in order.Items)
    {
        Console.WriteLine($"     - {OrderItemMapper.GetItemSummary(item)}");
        Console.WriteLine($"       Valid Item: {OrderItemMapper.IsValidItem(item)}");
    }
    Console.WriteLine();
}

// Demonstrate advanced mapping service functionality
Console.WriteLine("5. Advanced Mapping Service Operations:");
var customerLifetimeValue = MappingService.CalculateCustomerLifetimeValue(person);
var orderCount = MappingService.GetCustomerOrderCount(person);
var lastOrderDate = MappingService.GetLastOrderDate(person);

Console.WriteLine($"   Customer Lifetime Value: {customerLifetimeValue:C}");
Console.WriteLine($"   Total Orders: {orderCount}");
Console.WriteLine($"   Last Order Date: {lastOrderDate:yyyy-MM-dd}");
Console.WriteLine($"   Is Adult: {MappingService.IsAdult(person)}");
Console.WriteLine($"   Is Valid Person: {MappingService.IsValidPerson(person)}");

// Demonstrate filtering and conditional mapping
Console.WriteLine("\n6. Filtering and Conditional Mapping:");
var persons = new List<Person> { person };
var adultPersons = MappingService.MapAdultPersons(persons);
Console.WriteLine($"   Adult Persons Mapped: {adultPersons.Count()}");

var activeOrders = MappingService.MapActiveOrders(person.CustomerOrders);
Console.WriteLine($"   Active Orders: {activeOrders.Count()}");

// Demonstrate summary mapping
Console.WriteLine("\n7. Summary Mapping:");
var personSummary = MappingService.MapPersonSummary(person);
Console.WriteLine($"   Summary - Name: {personSummary.FirstName} {personSummary.LastName}");
Console.WriteLine($"   Primary Phones: {personSummary.PhoneNumbers.Count}");
Console.WriteLine($"   Recent Orders: {personSummary.Orders.Count}");

// Demonstrate bulk operations
Console.WriteLine("\n8. Bulk Operations:");
var personList = new List<Person> { person };
var dtoList = PersonMapper.ToDtoList(personList);
Console.WriteLine($"   Bulk mapped DTOs: {dtoList.Count()}");

var mappedBackList = PersonMapper.ToEntityList(dtoList);
Console.WriteLine($"   Bulk mapped back to entities: {mappedBackList.Count()}");

// Demonstrate high-value item filtering
Console.WriteLine("\n9. Advanced Item Filtering:");
var allItems = personDto.Orders.SelectMany(o => o.Items);
var highValueItems = OrderItemMapper.GetHighValueItems(allItems, 75.00m);
Console.WriteLine($"   High-value items (>$75): {highValueItems.Count()}");
foreach (var item in highValueItems)
{
    Console.WriteLine($"     - {OrderItemMapper.FormatItemDescription(item)}");
}

var averageItemValue = OrderItemMapper.GetAverageItemValue(allItems);
Console.WriteLine($"   Average item value: {averageItemValue:C}");

Console.WriteLine("\n=== Mapping Demonstration Complete ===");
