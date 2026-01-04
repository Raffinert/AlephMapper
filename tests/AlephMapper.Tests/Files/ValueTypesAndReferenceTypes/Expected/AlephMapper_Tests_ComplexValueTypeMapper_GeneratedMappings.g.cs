using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class ComplexValueTypeMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestination(ComplexValueTypeSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ComplexValueTypeSource, ComplexValueTypeDestination>> MapToDestinationExpression() => 
        source => new ComplexValueTypeDestination
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
