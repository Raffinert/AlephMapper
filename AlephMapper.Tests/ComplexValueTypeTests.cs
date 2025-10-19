namespace AlephMapper.Tests;

public class ComplexValueTypeTests
{
    [Test]
    public async Task Should_Handle_Complex_Nested_Value_Types()
    {
        // Arrange
        var source = new ComplexValueTypeSource
        {
            Id = 1,
            Name = "Test Entity",
            Address = new AddressStruct
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001",
                Coordinates = new CoordinatesStruct
                {
                    Latitude = 40.7128,
                    Longitude = -74.0060,
                    Elevation = 10.5f
                }
            },
            ContactInfo = new ContactInfoStruct
            {
                Email = "test@example.com",
                Phone = "555-0123",
                PreferredMethod = ContactMethod.Email,
                EmergencyContact = new EmergencyContactStruct
                {
                    Name = "John Doe",
                    Relationship = "Spouse",
                    Phone = "555-0456"
                }
            },
            Metadata = new MetadataStruct
            {
                CreatedAt = new DateTime(2023, 1, 1),
                UpdatedAt = new DateTime(2023, 6, 15),
                Version = 1,
                Tags = new TagsStruct
                {
                    Primary = "important",
                    Secondary = "test",
                    Category = TagCategory.Personal
                }
            }
        };

        // Act - Use single-parameter method since Updatable methods aren't generated for value types
        var destination = ComplexValueTypeMapper.MapToDestination(source);

        // Assert - Verify all properties are correctly mapped
        await Assert.That(destination.Id).IsEqualTo(1);
        await Assert.That(destination.Name).IsEqualTo("Test Entity");

        // Address verification
        await Assert.That(destination.Address.Street).IsEqualTo("123 Main St");
        await Assert.That(destination.Address.City).IsEqualTo("New York");
        await Assert.That(destination.Address.ZipCode).IsEqualTo("10001");

        // Nested coordinates verification
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(40.7128);
        await Assert.That(destination.Address.Coordinates.Longitude).IsEqualTo(-74.0060);
        await Assert.That(destination.Address.Coordinates.Elevation).IsEqualTo(10.5f);

        // Contact info verification
        await Assert.That(destination.ContactInfo.Email).IsEqualTo("test@example.com");
        await Assert.That(destination.ContactInfo.Phone).IsEqualTo("555-0123");
        await Assert.That(destination.ContactInfo.PreferredMethod).IsEqualTo(ContactMethod.Email);

        // Emergency contact verification
        await Assert.That(destination.ContactInfo.EmergencyContact.Name).IsEqualTo("John Doe");
        await Assert.That(destination.ContactInfo.EmergencyContact.Relationship).IsEqualTo("Spouse");
        await Assert.That(destination.ContactInfo.EmergencyContact.Phone).IsEqualTo("555-0456");

        // Metadata verification
        await Assert.That(destination.Metadata.CreatedAt).IsEqualTo(new DateTime(2023, 1, 1));
        await Assert.That(destination.Metadata.UpdatedAt).IsEqualTo(new DateTime(2023, 6, 15));
        await Assert.That(destination.Metadata.Version).IsEqualTo(1);

        // Tags verification
        await Assert.That(destination.Metadata.Tags.Primary).IsEqualTo("important");
        await Assert.That(destination.Metadata.Tags.Secondary).IsEqualTo("test");
        await Assert.That(destination.Metadata.Tags.Category).IsEqualTo(TagCategory.Personal);
    }

    [Test]
    public async Task Should_Handle_Default_Values_For_Complex_Value_Types()
    {
        // Test with default/empty source
        var source = new ComplexValueTypeSource(); // All default values

        var destination = ComplexValueTypeMapper.MapToDestination(source);

        // Should handle default values correctly
        await Assert.That(destination.Id).IsEqualTo(0);
        await Assert.That(destination.Name).IsNull(); // string is null by default
        await Assert.That(destination.Address.Street).IsNull();
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(0.0);
        await Assert.That(destination.ContactInfo.PreferredMethod).IsEqualTo(ContactMethod.Email); // enum default
        await Assert.That(destination.Metadata.CreatedAt).IsEqualTo(default(DateTime));
    }

    [Test]
    public async Task Should_Handle_Partial_Data_In_Complex_Value_Types()
    {
        // Test with partially filled data
        var source = new ComplexValueTypeSource
        {
            Id = 42,
            Name = "Partial Test",
            Address = new AddressStruct
            {
                Street = "456 Oak Ave",
                // City and ZipCode left as default
                Coordinates = new CoordinatesStruct
                {
                    Latitude = 34.0522,
                    // Longitude and Elevation left as default
                }
            },
            // ContactInfo left as default
            Metadata = new MetadataStruct
            {
                Version = 2,
                // Other fields left as default
                Tags = new TagsStruct
                {
                    Primary = "partial",
                    Category = TagCategory.Work
                    // Secondary left as default
                }
            }
        };

        var destination = ComplexValueTypeMapper.MapToDestination(source);

        // Verify partial mapping
        await Assert.That(destination.Id).IsEqualTo(42);
        await Assert.That(destination.Name).IsEqualTo("Partial Test");
        await Assert.That(destination.Address.Street).IsEqualTo("456 Oak Ave");
        await Assert.That(destination.Address.City).IsNull(); // Default
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(34.0522);
        await Assert.That(destination.Address.Coordinates.Longitude).IsEqualTo(0.0); // Default
        await Assert.That(destination.ContactInfo.Email).IsNull(); // Default
        await Assert.That(destination.Metadata.Version).IsEqualTo(2);
        await Assert.That(destination.Metadata.Tags.Primary).IsEqualTo("partial");
        await Assert.That(destination.Metadata.Tags.Secondary).IsNull(); // Default
        await Assert.That(destination.Metadata.Tags.Category).IsEqualTo(TagCategory.Work);
    }
}

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