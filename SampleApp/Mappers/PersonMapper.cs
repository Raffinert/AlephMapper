using AlephMapper;
using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

public static partial class PersonMapper
{
    // Main entity to DTO mapping with expression-bodied syntax
    //[Expressive]
    [Updatable(CollectionProperties = CollectionPropertiesPolicy.Skip)]
    public static PersonDto ToDto(Person entity) => entity == null ? null : new()
    {
        Id = entity.PersonId,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        Email = entity.EmailAddress,
        DateOfBirth = entity.BirthDate,
        Address = entity.HomeAddress.ToDto(),
        PhoneNumbers = entity.ContactNumbers.Select(cn => cn.ToDto()).ToList(),
        Orders = entity.CustomerOrders.Select(OrderMapper.ToDto).ToList()
    };

    // DTO to entity mapping with expression-bodied syntax
    //[Updatable]
    public static Person ToEntity(PersonDto dto) => new()
    {
        PersonId = dto.Id,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        EmailAddress = dto.Email,
        BirthDate = dto.DateOfBirth,
        HomeAddress = AddressMapper.ToEntity(dto.Address),
        ContactNumbers = dto.PhoneNumbers.Select(PhoneMapper.ToEntity).ToList(),
        CustomerOrders = dto.Orders.Select(OrderMapper.ToEntity).ToList()
    };

    // Bulk mapping operations with expression-bodied syntax
    public static IEnumerable<PersonDto> ToDtoList(IEnumerable<Person> entities) =>
        entities.Select(ToDto);

    public static IEnumerable<Person> ToEntityList(IEnumerable<PersonDto> dtos) =>
        dtos.Select(ToEntity);

    // Conditional mapping with expression-bodied syntax
    public static PersonDto? ToDtoSafe(Person? entity) =>
        entity is null ? null : ToDto(entity);

    public static Person? ToEntitySafe(PersonDto? dto) =>
        dto is null ? null : ToEntity(dto);

    // Partial mapping for updates with expression-bodied syntax
    public static void UpdateEntityFromDto(Person entity, PersonDto dto) =>
        UpdateEntity(entity, dto.FirstName, dto.LastName, dto.Email, dto.DateOfBirth);

    // Helper method with expression-bodied syntax for partial updates
    private static void UpdateEntity(Person entity, string firstName, string lastName, string email, DateTime birthDate)
    {
        entity.FirstName = firstName;
        entity.LastName = lastName;
        entity.EmailAddress = email;
        entity.BirthDate = birthDate;
    }
}