namespace AlephMapper.Tests;

public static class Mapper1
{
    public static bool Older35(BirthInfo? source) => source?.Age > 35;
}

[Expressive] // Using default behavior (now Ignore)
public static partial class Mapper
{
    public static bool LivesInKyivAndOlder35(SourceDto source) => BornInKyiv(source.BirthInfo) && Mapper1.Older35(source.BirthInfo) && Yanger65(source.BirthInfo);

    public static bool BornInKyiv(BirthInfo? source) => source?.Address == "Kyiv";


    public static bool Yanger65(BirthInfo? source) => source?.Age < 65;

    public static bool LivesIn(BirthInfo source) => source.Address == "Kyiv";
    public static DestDto MapToDestDto(SourceDto source) => new DestDto
    {
        Name = source.Name,
        BirthInfo = source.BirthInfo != null ? MapToBirthInfoDto(source.BirthInfo) : null,
        ContactInfo = source.Email
    };

    public static BirthInfoDto MapToBirthInfoDto(BirthInfo bi) => new BirthInfoDto
    {
        Age = bi.Age,
        Address = bi.Address
    };
}