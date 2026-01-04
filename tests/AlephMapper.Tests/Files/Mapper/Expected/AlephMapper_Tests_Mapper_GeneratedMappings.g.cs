using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class Mapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="BornInKyivAndOlder35(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, bool>> BornInKyivAndOlder35Expression() => 
        source => source.BirthInfo.Address == "Kyiv" && source.BirthInfo.Age > 35 && source.BirthInfo.Age < 65;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="BornInKyiv(BirthInfo)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<BirthInfo, bool>> BornInKyivExpression() => 
        source => source.Address == "Kyiv";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Younger65(BirthInfo)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<BirthInfo, bool>> Younger65Expression() => 
        source => source.Age < 65;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="LivesIn(BirthInfo)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<BirthInfo, bool>> LivesInExpression() => 
        source => source.Address == "Kyiv";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestDto(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, DestDto>> MapToDestDtoExpression() => 
        source => new DestDto
        {
            Name = source.Name,
            BirthInfo = source.BirthInfo != null
                ? new BirthInfoDto
                {
                    Age = source.BirthInfo.Age,
                    Address = source.BirthInfo.Address
                }
                : null,
            ContactInfo = source.Email
        };

    /// <summary>
    /// Updates an existing instance of <see cref="DestDto"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestDto MapToDestDto(SourceDto source, DestDto dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestDto();
        dest.Name = source.Name;
        if (source.BirthInfo != null)
        {
            if (dest.BirthInfo == null)
                dest.BirthInfo = new BirthInfoDto();
            dest.BirthInfo.Age = source.BirthInfo.Age;
            dest.BirthInfo.Address = source.BirthInfo.Address;
        }
        else
        {
            dest.BirthInfo = null;
        }
        dest.ContactInfo = source.Email;
        return dest;
    }

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDestDto1(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, DestDto>> MapToDestDto1Expression() => 
        source => new DestDto
        {
            Name = source.Name,
            BirthInfo = source.BirthInfo == null
                ? null 
                : new BirthInfoDto
                {
                    Age = source.BirthInfo.Age,
                    Address = source.BirthInfo.Address
                },
            ContactInfo = source.Email
        };

    /// <summary>
    /// Updates an existing instance of <see cref="DestDto"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static DestDto MapToDestDto1(SourceDto source, DestDto dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new DestDto();
        dest.Name = source.Name;
        if (source.BirthInfo == null)
        {
            dest.BirthInfo = null;
        }
        else
        {
            if (dest.BirthInfo == null)
                dest.BirthInfo = new BirthInfoDto();
            dest.BirthInfo.Age = source.BirthInfo.Age;
            dest.BirthInfo.Address = source.BirthInfo.Address;
        }
        dest.ContactInfo = source.Email;
        return dest;
    }

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToBirthInfoDto(BirthInfo)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<BirthInfo, BirthInfoDto>> MapToBirthInfoDtoExpression() => 
        bi => new BirthInfoDto
        {
            Age = bi.Age,
            Address = bi.Address
        };
}
