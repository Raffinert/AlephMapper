using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class ValueToReferenceMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToReferenceDestination(ComplexValueTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ComplexValueTypeSource, ComplexReferenceTypeDestination>> MapToReferenceDestinationExpression() => 
        source => new ComplexReferenceTypeDestination
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

    /// <summary>
    /// Updates an existing instance of <see cref="ComplexReferenceTypeDestination"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static ComplexReferenceTypeDestination MapToReferenceDestination(ComplexValueTypeSource source, ComplexReferenceTypeDestination dest)
    {
        if (dest == null)
            dest = new ComplexReferenceTypeDestination();
        dest.Id = source.Id;
        dest.Name = source.Name;
        if (dest.Address == null)
            dest.Address = new AddressClass();
        dest.Address.Street = source.Address.Street;
        dest.Address.City = source.Address.City;
        dest.Address.ZipCode = source.Address.ZipCode;
        if (dest.Address.Coordinates == null)
            dest.Address.Coordinates = new CoordinatesClass();
        dest.Address.Coordinates.Latitude = source.Address.Coordinates.Latitude;
        dest.Address.Coordinates.Longitude = source.Address.Coordinates.Longitude;
        dest.Address.Coordinates.Elevation = source.Address.Coordinates.Elevation;
        if (dest.ContactInfo == null)
            dest.ContactInfo = new ContactInfoClass();
        dest.ContactInfo.Email = source.ContactInfo.Email;
        dest.ContactInfo.Phone = source.ContactInfo.Phone;
        dest.ContactInfo.PreferredMethod = source.ContactInfo.PreferredMethod;
        if (dest.ContactInfo.EmergencyContact == null)
            dest.ContactInfo.EmergencyContact = new EmergencyContactClass();
        dest.ContactInfo.EmergencyContact.Name = source.ContactInfo.EmergencyContact.Name;
        dest.ContactInfo.EmergencyContact.Relationship = source.ContactInfo.EmergencyContact.Relationship;
        dest.ContactInfo.EmergencyContact.Phone = source.ContactInfo.EmergencyContact.Phone;
        if (dest.Metadata == null)
            dest.Metadata = new MetadataClass();
        dest.Metadata.CreatedAt = source.Metadata.CreatedAt;
        dest.Metadata.UpdatedAt = source.Metadata.UpdatedAt;
        dest.Metadata.Version = source.Metadata.Version;
        if (dest.Metadata.Tags == null)
            dest.Metadata.Tags = new TagsClass();
        dest.Metadata.Tags.Primary = source.Metadata.Tags.Primary;
        dest.Metadata.Tags.Secondary = source.Metadata.Tags.Secondary;
        dest.Metadata.Tags.Category = source.Metadata.Tags.Category;
        return dest;
    }
}
