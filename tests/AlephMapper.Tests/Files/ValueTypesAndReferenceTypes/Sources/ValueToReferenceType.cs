namespace AlephMapper.Tests;

// Reference type equivalents of the value types
internal class ComplexReferenceTypeDestination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AddressClass Address { get; set; }
    public ContactInfoClass ContactInfo { get; set; }
    public MetadataClass Metadata { get; set; }
}

internal class AddressClass
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
    public CoordinatesClass Coordinates { get; set; }
}

internal class CoordinatesClass
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Elevation { get; set; }
}

internal class ContactInfoClass
{
    public string Email { get; set; }
    public string Phone { get; set; }
    public ContactMethod PreferredMethod { get; set; }
    public EmergencyContactClass EmergencyContact { get; set; }
}

internal class EmergencyContactClass
{
    public string Name { get; set; }
    public string Relationship { get; set; }
    public string Phone { get; set; }
}

internal class MetadataClass
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public TagsClass Tags { get; set; }
}

internal class TagsClass
{
    public string Primary { get; set; }
    public string Secondary { get; set; }
    public TagCategory Category { get; set; }
}

[Expressive]
internal static partial class ValueToReferenceMapper
{
    [Updatable]
    public static ComplexReferenceTypeDestination MapToReferenceDestination(ComplexValueTypeSource source)
        => new ComplexReferenceTypeDestination
        {
            Id = source.Id,
            Name = source.Name,
            Address = new AddressClass
            {
                Street = source.Address.Street,
                City = source.Address.City,
                ZipCode = source.Address.ZipCode,
                Coordinates = new CoordinatesClass
                {
                    Latitude = source.Address.Coordinates.Latitude,
                    Longitude = source.Address.Coordinates.Longitude,
                    Elevation = source.Address.Coordinates.Elevation
                }
            },
            ContactInfo = new ContactInfoClass
            {
                Email = source.ContactInfo.Email,
                Phone = source.ContactInfo.Phone,
                PreferredMethod = source.ContactInfo.PreferredMethod,
                EmergencyContact = new EmergencyContactClass
                {
                    Name = source.ContactInfo.EmergencyContact.Name,
                    Relationship = source.ContactInfo.EmergencyContact.Relationship,
                    Phone = source.ContactInfo.EmergencyContact.Phone
                }
            },
            Metadata = new MetadataClass
            {
                CreatedAt = source.Metadata.CreatedAt,
                UpdatedAt = source.Metadata.UpdatedAt,
                Version = source.Metadata.Version,
                Tags = new TagsClass
                {
                    Primary = source.Metadata.Tags.Primary,
                    Secondary = source.Metadata.Tags.Secondary,
                    Category = source.Metadata.Tags.Category
                }
            }
        };
}