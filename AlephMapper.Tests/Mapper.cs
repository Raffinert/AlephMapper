namespace AlephMapper.Tests;

internal static class Mapper1
{
    public static bool Older35(BirthInfo? source) => source?.Age > 35;
}

[Expressive]
internal static partial class Mapper
{
    public static bool BornInKyivAndOlder35(SourceDto source) => BornInKyiv(source.BirthInfo) && Mapper1.Older35(source.BirthInfo) && Younger65(source.BirthInfo);

    public static bool BornInKyiv(BirthInfo? source) => source?.Address == "Kyiv";

    public static bool Younger65(BirthInfo? source) => source?.Age < 65;

    public static bool LivesIn(BirthInfo source) => source.Address == "Kyiv";

    [Updateable]
    public static DestDto MapToDestDto(SourceDto source) => new DestDto
    {
        Name = source.Name,
        BirthInfo = source.BirthInfo != null ? MapToBirthInfoDto(source.BirthInfo) : null,
        ContactInfo = source.Email
    };

    [Updateable]
    public static DestDto MapToDestDto1(SourceDto source) => new DestDto
    {
        Name = source.Name,
        BirthInfo = source.BirthInfo == null ? null : MapToBirthInfoDto(source.BirthInfo),
        ContactInfo = source.Email
    };

    public static BirthInfoDto MapToBirthInfoDto(BirthInfo bi) => new BirthInfoDto
    {
        Age = bi.Age,
        Address = bi.Address
    };
}