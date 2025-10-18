using TUnit.Core;

namespace AlephMapper.Tests;

public class ValueTypeToReferenceTypeTests
{
    [Test]
    public async Task Should_Generate_Updateable_Method_For_Reference_Type_Destination()
    {
        // This test verifies that when mapping FROM value type TO reference type,
        // the updateable method should be generated (because destination is reference type)
            
        var source = new ComplexValueTypeSource
        {
            Id = 1,
            Name = "Test Entity"
        };

        var destination = new ComplexReferenceTypeDestination();
            
        // This call should work because destination is reference type, so updateable method should be generated
        // Even though source is value type, the decision is based on the RETURN type (destination)
        ValueToReferenceMapper.MapToReferenceDestination(source, destination);

        await Assert.That(destination.Id).IsEqualTo(1);
        await Assert.That(destination.Name).IsEqualTo("Test Entity");
    }

    [Test]
    public async Task Should_Handle_Complex_Value_Type_To_Reference_Type_Mapping()
    {
        // Arrange - Create a complex value type source
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

        // Act - Map to reference type (this should generate updateable method)
        var destination = new ComplexReferenceTypeDestination();
        ValueToReferenceMapper.MapToReferenceDestination(source, destination);

        // Assert - Verify all properties are correctly mapped
        await Assert.That(destination.Id).IsEqualTo(1);
        await Assert.That(destination.Name).IsEqualTo("Test Entity");
            
        // Address verification (should be reference type)
        await Assert.That(destination.Address).IsNotNull();
        await Assert.That(destination.Address.Street).IsEqualTo("123 Main St");
        await Assert.That(destination.Address.City).IsEqualTo("New York");
        await Assert.That(destination.Address.ZipCode).IsEqualTo("10001");
            
        // Nested coordinates verification (should be reference type)
        await Assert.That(destination.Address.Coordinates).IsNotNull();
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(40.7128);
        await Assert.That(destination.Address.Coordinates.Longitude).IsEqualTo(-74.0060);
        await Assert.That(destination.Address.Coordinates.Elevation).IsEqualTo(10.5f);
            
        // Contact info verification (should be reference type)
        await Assert.That(destination.ContactInfo).IsNotNull();
        await Assert.That(destination.ContactInfo.Email).IsEqualTo("test@example.com");
        await Assert.That(destination.ContactInfo.Phone).IsEqualTo("555-0123");
        await Assert.That(destination.ContactInfo.PreferredMethod).IsEqualTo(ContactMethod.Email);
            
        // Emergency contact verification (should be reference type)
        await Assert.That(destination.ContactInfo.EmergencyContact).IsNotNull();
        await Assert.That(destination.ContactInfo.EmergencyContact.Name).IsEqualTo("John Doe");
        await Assert.That(destination.ContactInfo.EmergencyContact.Relationship).IsEqualTo("Spouse");
        await Assert.That(destination.ContactInfo.EmergencyContact.Phone).IsEqualTo("555-0456");
            
        // Metadata verification (should be reference type)
        await Assert.That(destination.Metadata).IsNotNull();
        await Assert.That(destination.Metadata.CreatedAt).IsEqualTo(new DateTime(2023, 1, 1));
        await Assert.That(destination.Metadata.UpdatedAt).IsEqualTo(new DateTime(2023, 6, 15));
        await Assert.That(destination.Metadata.Version).IsEqualTo(1);
            
        // Tags verification (should be reference type)
        await Assert.That(destination.Metadata.Tags).IsNotNull();
        await Assert.That(destination.Metadata.Tags.Primary).IsEqualTo("important");
        await Assert.That(destination.Metadata.Tags.Secondary).IsEqualTo("test");
        await Assert.That(destination.Metadata.Tags.Category).IsEqualTo(TagCategory.Personal);
    }

    [Test]
    public async Task Should_Handle_Null_Source_For_Value_To_Reference_Mapping()
    {
        // This tests the edge case where source is a value type (can't be null)
        // but destination is reference type (can be null)
            
        var source = new ComplexValueTypeSource(); // Default values
        var destination = new ComplexReferenceTypeDestination();

        ValueToReferenceMapper.MapToReferenceDestination(source, destination);

        // Should handle default values correctly and create reference objects
        await Assert.That(destination.Id).IsEqualTo(0);
        await Assert.That(destination.Name).IsNull(); // string is null by default
            
        // These should be created as new objects (not null) even though source has default values
        await Assert.That(destination.Address).IsNotNull();
        await Assert.That(destination.Address.Street).IsNull();
        await Assert.That(destination.Address.Coordinates).IsNotNull();
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(0.0);
            
        await Assert.That(destination.ContactInfo).IsNotNull();
        await Assert.That(destination.ContactInfo.Email).IsNull();
        await Assert.That(destination.ContactInfo.EmergencyContact).IsNotNull();
            
        await Assert.That(destination.Metadata).IsNotNull();
        await Assert.That(destination.Metadata.CreatedAt).IsEqualTo(default(DateTime));
        await Assert.That(destination.Metadata.Tags).IsNotNull();
    }

    [Test]
    public async Task Should_Update_Existing_Reference_Objects()
    {
        // Test that existing reference objects get updated, not replaced
        var existingAddress = new AddressClass { Street = "Old Street", City = "Old City" };
        var existingCoordinates = new CoordinatesClass { Latitude = 999.0, Longitude = 888.0 };
        existingAddress.Coordinates = existingCoordinates;

        var destination = new ComplexReferenceTypeDestination
        {
            Id = 999,
            Name = "Old Name",
            Address = existingAddress
        };

        var source = new ComplexValueTypeSource
        {
            Id = 42,
            Name = "New Name",
            Address = new AddressStruct
            {
                Street = "New Street",
                City = "New City",
                ZipCode = "12345",
                Coordinates = new CoordinatesStruct
                {
                    Latitude = 11.11,
                    Longitude = 22.22,
                    Elevation = 33.33f
                }
            }
        };

        ValueToReferenceMapper.MapToReferenceDestination(source, destination);

        // Verify existing objects were reused and updated
        await Assert.That(destination.Address).IsSameReferenceAs(existingAddress);
        await Assert.That(destination.Address.Coordinates).IsSameReferenceAs(existingCoordinates);
            
        // Verify values were updated
        await Assert.That(destination.Id).IsEqualTo(42);
        await Assert.That(destination.Name).IsEqualTo("New Name");
        await Assert.That(destination.Address.Street).IsEqualTo("New Street");
        await Assert.That(destination.Address.City).IsEqualTo("New City");
        await Assert.That(destination.Address.ZipCode).IsEqualTo("12345");
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(11.11);
        await Assert.That(destination.Address.Coordinates.Longitude).IsEqualTo(22.22);
        await Assert.That(destination.Address.Coordinates.Elevation).IsEqualTo(33.33f);
    }

    [Test]
    public async Task Should_Handle_Complex_Nested_Property_Assignments()
    {
        // This test specifically checks that complex nested value type to reference type
        // assignments work correctly, particularly with the EmitHelpers logic
            
        var source = new ComplexValueTypeSource
        {
            Address = new AddressStruct
            {
                Coordinates = new CoordinatesStruct
                {
                    Latitude = 12.34,
                    Longitude = 56.78,
                    Elevation = 90.12f
                }
            }
        };

        var destination = new ComplexReferenceTypeDestination();

        // This should work without issues - the generator should handle the deep nesting
        ValueToReferenceMapper.MapToReferenceDestination(source, destination);

        await Assert.That(destination.Address).IsNotNull();
        await Assert.That(destination.Address.Coordinates).IsNotNull();
        await Assert.That(destination.Address.Coordinates.Latitude).IsEqualTo(12.34);
        await Assert.That(destination.Address.Coordinates.Longitude).IsEqualTo(56.78);
        await Assert.That(destination.Address.Coordinates.Elevation).IsEqualTo(90.12f);
    }
}

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
    [Updateable]
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