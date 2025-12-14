namespace AlephMapper.Tests;

// Complex nested value types
internal struct ComplexValueTypeSource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AddressStruct Address { get; set; }
    public ContactInfoStruct ContactInfo { get; set; }
    public MetadataStruct Metadata { get; set; }
}

internal struct ComplexValueTypeDestination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AddressStruct Address { get; set; }
    public ContactInfoStruct ContactInfo { get; set; }
    public MetadataStruct Metadata { get; set; }
}

internal struct AddressStruct
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
    public CoordinatesStruct Coordinates { get; set; }
}

internal struct CoordinatesStruct
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Elevation { get; set; }
}

internal struct ContactInfoStruct
{
    public string Email { get; set; }
    public string Phone { get; set; }
    public ContactMethod PreferredMethod { get; set; }
    public EmergencyContactStruct EmergencyContact { get; set; }
}

internal struct EmergencyContactStruct
{
    public string Name { get; set; }
    public string Relationship { get; set; }
    public string Phone { get; set; }
}

internal struct MetadataStruct
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public TagsStruct Tags { get; set; }
}

internal struct TagsStruct
{
    public string Primary { get; set; }
    public string Secondary { get; set; }
    public TagCategory Category { get; set; }
}

internal enum ContactMethod
{
    Email = 0,
    Phone = 1,
    Mail = 2
}

internal enum TagCategory
{
    Personal = 0,
    Work = 1,
    Other = 2
}

[Expressive]
internal static partial class ComplexValueTypeMapper
{
    [Updatable]
    public static ComplexValueTypeDestination MapToDestination(ComplexValueTypeSource source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Address = new AddressStruct
            {
                Street = source.Address.Street,
                City = source.Address.City,
                ZipCode = source.Address.ZipCode,
                Coordinates = new CoordinatesStruct
                {
                    Latitude = source.Address.Coordinates.Latitude,
                    Longitude = source.Address.Coordinates.Longitude,
                    Elevation = source.Address.Coordinates.Elevation
                }
            },
            ContactInfo = new ContactInfoStruct
            {
                Email = source.ContactInfo.Email,
                Phone = source.ContactInfo.Phone,
                PreferredMethod = source.ContactInfo.PreferredMethod,
                EmergencyContact = new EmergencyContactStruct
                {
                    Name = source.ContactInfo.EmergencyContact.Name,
                    Relationship = source.ContactInfo.EmergencyContact.Relationship,
                    Phone = source.ContactInfo.EmergencyContact.Phone
                }
            },
            Metadata = new MetadataStruct
            {
                CreatedAt = source.Metadata.CreatedAt,
                UpdatedAt = source.Metadata.UpdatedAt,
                Version = source.Metadata.Version,
                Tags = new TagsStruct
                {
                    Primary = source.Metadata.Tags.Primary,
                    Secondary = source.Metadata.Tags.Secondary,
                    Category = source.Metadata.Tags.Category
                }
            }
        };
}