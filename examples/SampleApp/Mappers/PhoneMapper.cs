using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

public static class PhoneMapper
{
    // Entity to DTO mapping with expression-bodied syntax
    public static PhoneDto ToDto(this PhoneNumber entity) => new()
    {
        Type = GetPhoneTypeString(entity.PhoneType),
        Number = FormatPhoneNumber(entity.PhoneNumberValue)
    };

    // DTO to entity mapping with expression-bodied syntax
    public static PhoneNumber ToEntity(PhoneDto dto) => new()
    {
        PhoneType = ParsePhoneType(dto.Type),
        PhoneNumberValue = CleanPhoneNumber(dto.Number)
    };

    // Bulk mapping operations with expression-bodied syntax
    public static IEnumerable<PhoneDto> ToDtoList(IEnumerable<PhoneNumber> entities) =>
        entities.Select(ToDto);

    public static IEnumerable<PhoneNumber> ToEntityList(IEnumerable<PhoneDto> dtos) =>
        dtos.Select(ToEntity);

    // Phone type conversion with expression-bodied syntax
    public static string GetPhoneTypeString(PhoneType phoneType) => phoneType switch
    {
        PhoneType.Mobile => "Mobile",
        PhoneType.Home => "Home",
        PhoneType.Work => "Work",
        PhoneType.Fax => "Fax",
        _ => "Unknown"
    };

    public static PhoneType ParsePhoneType(string phoneTypeString) => phoneTypeString.ToLower() switch
    {
        "mobile" => PhoneType.Mobile,
        "home" => PhoneType.Home,
        "work" => PhoneType.Work,
        "fax" => PhoneType.Fax,
        _ => PhoneType.Mobile
    };

    // Phone number formatting with expression-bodied syntax
    public static string FormatPhoneNumber(string phoneNumber) =>
        phoneNumber == null
        || phoneNumber == ""
            ? string.Empty
            : phoneNumber.Length == 10
                ? $"({phoneNumber.Take(3)}) {phoneNumber.Skip(3).Take(3)}-{phoneNumber.Skip(6)}"
                : phoneNumber;

    // Phone number cleaning with expression-bodied syntax
    public static string CleanPhoneNumber(string phoneNumber) =>
        new string(phoneNumber.Where(char.IsDigit).ToArray());

    // Validation with expression-bodied syntax
    public static bool IsValidPhoneNumber(string phoneNumber) =>
        !string.IsNullOrWhiteSpace(phoneNumber) &&
        CleanPhoneNumber(phoneNumber).Length >= 10;

    public static bool IsValidPhoneDto(PhoneDto dto) =>
        !string.IsNullOrWhiteSpace(dto.Type) &&
        IsValidPhoneNumber(dto.Number);
}