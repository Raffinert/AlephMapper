using SampleApp.Entities;
using SampleApp.Mappers;
using SampleApp.Models;
using PersonMapper = SampleApp.Mappers.PersonMapper;

namespace SampleApp.Services;

public static class MappingService
{
    // Generic mapping service with expression-bodied syntax
    public static TDestination Map<TSource, TDestination>(TSource source, Func<TSource, TDestination> mapper) =>
        mapper(source);

    // Batch mapping with expression-bodied syntax
    public static IEnumerable<TDestination> MapCollection<TSource, TDestination>(
        IEnumerable<TSource> sources,
        Func<TSource, TDestination> mapper) =>
        sources.Select(mapper);

    // Safe mapping with null handling using expression-bodied syntax
    public static TDestination? MapSafe<TSource, TDestination>(
        TSource? source,
        Func<TSource, TDestination> mapper) where TSource : class where TDestination : class =>
        source is null ? null : mapper(source);

    // Complex person mapping with validation using expression-bodied syntax
    public static PersonDto? MapPersonWithValidation(Person? person) =>
        person is null ? null :
        IsValidPerson(person) ? PersonMapper.ToDto(person) :
        throw new ArgumentException("Invalid person data");

    // Conditional mapping based on business rules with expression-bodied syntax
    public static IEnumerable<OrderDto> MapActiveOrders(IEnumerable<Order> orders) =>
        orders.Where(IsActiveOrder)
              .Select(OrderMapper.ToDto);

    public static IEnumerable<PersonDto> MapAdultPersons(IEnumerable<Person> persons) =>
        persons.Where(IsAdult)
               .Select(PersonMapper.ToDto);

    // Validation helpers with expression-bodied syntax
    public static bool IsValidPerson(Person person) =>
        !string.IsNullOrWhiteSpace(person.FirstName) &&
        !string.IsNullOrWhiteSpace(person.LastName) &&
        !string.IsNullOrWhiteSpace(person.EmailAddress) &&
        person.BirthDate != default;

    public static bool IsActiveOrder(Order order) =>
        order.OrderStatus is not OrderStatus.Cancelled and not OrderStatus.Delivered;

    public static bool IsAdult(Person person) =>
        DateTime.Now.Year - person.BirthDate.Year >= 18;

    // Advanced mapping scenarios with expression-bodied syntax
    public static PersonDto MapPersonSummary(Person person) => new()
    {
        Id = person.PersonId,
        FirstName = person.FirstName,
        LastName = person.LastName,
        Email = person.EmailAddress,
        DateOfBirth = person.BirthDate,
        Address = person.HomeAddress is null ? new AddressDto() : AddressMapper.ToDto(person.HomeAddress),
        PhoneNumbers = GetPrimaryPhones(person.ContactNumbers),
        Orders = GetRecentOrders(person.CustomerOrders)
    };

    // Helper methods for advanced mapping with expression-bodied syntax
    private static List<PhoneDto> GetPrimaryPhones(ICollection<PhoneNumber> phones) =>
        phones.Where(p => p.PhoneType is PhoneType.Mobile or PhoneType.Home)
              .Select(PhoneMapper.ToDto)
              .ToList();

    private static List<OrderDto> GetRecentOrders(ICollection<Order> orders) =>
        orders.Where(o => o.CreatedDate >= DateTime.Now.AddMonths(-6))
              .OrderByDescending(o => o.CreatedDate)
              .Take(5)
              .Select(OrderMapper.ToDto)
              .ToList();

    // Statistics and aggregation with expression-bodied syntax
    public static decimal CalculateCustomerLifetimeValue(Person person) =>
        person.CustomerOrders.Sum(o => o.Total);

    public static int GetCustomerOrderCount(Person person) =>
        person.CustomerOrders.Count;

    public static DateTime? GetLastOrderDate(Person person) =>
        person.CustomerOrders.Any() ? person.CustomerOrders.Max(o => o.CreatedDate) : null;
}