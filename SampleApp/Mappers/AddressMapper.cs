using AlephMapper;
using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

public static partial class AddressMapper
{
    // Entity to DTO mapping with expression-bodied syntax
    [Expressive]
    public static AddressDto ToDto(this Address entity) => new()
    {
        Street = entity.StreetAddress,
        City = entity.CityName,
        State = entity.StateName,
        ZipCode = entity.PostalCode,
        Country = entity.CountryName
    };

    // DTO to entity mapping with expression-bodied syntax
    public static Address ToEntity(AddressDto dto) => new()
    {
        StreetAddress = dto.Street,
        CityName = dto.City,
        StateName = dto.State,
        PostalCode = dto.ZipCode,
        CountryName = dto.Country
    };

    // Safe mapping with null checks using expression-bodied syntax
    public static AddressDto? ToDtoSafe(Address? entity) =>
        entity is null ? null : ToDto(entity);

    public static Address? ToEntitySafe(AddressDto? dto) =>
        dto is null ? null : ToEntity(dto);

    // Formatting helper with expression-bodied syntax
    public static string GetFormattedAddress(Address address) =>
        $"{address.StreetAddress}, {address.CityName}, {address.StateName} {address.PostalCode}, {address.CountryName}";

    public static string GetFormattedAddress(AddressDto address) =>
        $"{address.Street}, {address.City}, {address.State} {address.ZipCode}, {address.Country}";

    // Validation helper with expression-bodied syntax
    public static bool IsValid(AddressDto dto) =>
        !string.IsNullOrWhiteSpace(dto.Street) &&
        !string.IsNullOrWhiteSpace(dto.City) &&
        !string.IsNullOrWhiteSpace(dto.State) &&
        !string.IsNullOrWhiteSpace(dto.ZipCode) &&
        !string.IsNullOrWhiteSpace(dto.Country);
}