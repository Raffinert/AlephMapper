# Complex Manual Mapping Example with Expression-Bodied Static Classes

This project demonstrates a comprehensive manual mapping system using static classes and methods with expression-bodied syntax in C# 13.0 and .NET 9.

## Features

### 🎯 **Expression-Bodied Mapping Methods**
All mapping methods use modern C# expression-bodied member syntax (`=>`) for concise, readable code.

### 🏗️ **Complex Entity Relationships**
- **Person** with nested Address, PhoneNumbers collection, and Orders collection
- **Order** with OrderItems collection
- Multiple enum types with automatic conversion
- Bi-directional mapping (Entity ↔ DTO)

### 📦 **Static Mapper Classes**

#### `PersonMapper`
- Entity to DTO and DTO to Entity mapping
- Bulk operations with `IEnumerable<T>`
- Safe null-aware mapping
- Partial update functionality

#### `AddressMapper`
- Property name transformation (e.g., `StreetAddress` → `Street`)
- Address formatting utilities
- Validation helpers

#### `PhoneMapper`
- Enum to string conversion with switch expressions
- Phone number formatting and cleaning
- Type-safe phone type parsing

#### `OrderMapper`
- Order status enum handling
- Advanced filtering by date ranges and status
- Total value calculations
- Comprehensive validation

#### `OrderItemMapper`
- Line total calculations
- Discount calculations
- High-value item filtering
- Aggregation operations (sum, average, count)

### 🚀 **Advanced Mapping Service**
The `MappingService` provides:
- Generic mapping with `Func<TSource, TDestination>`
- Safe null-aware mapping with generic constraints
- Business rule-based filtering
- Summary mapping with selective data inclusion
- Statistical calculations (lifetime value, order counts)

## Key Expression-Bodied Examples

### Simple Property Mapping
```csharp
public static PersonDto ToDto(Person entity) => new()
{
    Id = entity.PersonId,
    FirstName = entity.FirstName,
    LastName = entity.LastName,
    Email = entity.EmailAddress,
    // ... more properties
};
```

### Collection Mapping
```csharp
public static IEnumerable<PersonDto> ToDtoList(IEnumerable<Person> entities) =>
    entities.Select(ToDto);
```

### Conditional Logic with Switch Expressions
```csharp
public static string GetPhoneTypeString(PhoneType phoneType) => phoneType switch
{
    PhoneType.Mobile => "Mobile",
    PhoneType.Home => "Home",
    PhoneType.Work => "Work",
    PhoneType.Fax => "Fax",
    _ => "Unknown"
};
```

### Complex Filtering and Transformations
```csharp
public static IEnumerable<OrderDto> GetRecentOrders(IEnumerable<Order> orders, int days) =>
    orders.Where(o => o.CreatedDate >= DateTime.Now.AddDays(-days))
          .Select(ToDto);
```

### Business Logic with Pattern Matching
```csharp
public static bool IsActiveOrder(Order order) =>
    order.OrderStatus is not OrderStatus.Cancelled and not OrderStatus.Delivered;
```

## Demonstration Features

The program demonstrates:

1. **Basic Entity-DTO Mapping** - Converting between domain entities and DTOs
2. **Reverse Mapping** - Converting DTOs back to entities
3. **Phone Number Processing** - Formatting, validation, and cleaning
4. **Order Calculations** - Total values, item counts, validation
5. **Advanced Service Operations** - Customer lifetime value, filtering
6. **Bulk Operations** - Mapping collections efficiently
7. **Conditional Mapping** - Adult persons, active orders
8. **Summary Mapping** - Selective data inclusion
9. **Advanced Filtering** - High-value items, product-specific items
10. **Statistical Analysis** - Averages, totals, counts

## Technical Highlights

- **C# 13.0 Features**: Expression-bodied members, pattern matching, switch expressions
- **.NET 9**: Modern runtime with performance optimizations
- **Static Classes**: No instantiation needed, purely functional approach
- **Generic Methods**: Type-safe operations with compile-time checking
- **LINQ Integration**: Seamless collection transformations
- **Null Safety**: Proper null handling throughout
- **Validation**: Comprehensive business rule validation
- **Formatting**: User-friendly string representations

## Sample Output

The demonstration produces detailed output showing:
- Person details with formatted addresses and phone numbers
- Order processing with calculations and validation
- Advanced filtering and statistical operations
- Performance metrics and validation results

This example showcases how to build robust, maintainable mapping systems using modern C# features while maintaining clean, readable code through expression-bodied syntax.