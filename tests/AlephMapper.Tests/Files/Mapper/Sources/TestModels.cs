namespace AlephMapper.Tests;

public class SourceDto
{
    public string Name { get; set; } = "";
    public BirthInfo? BirthInfo { get; set; }
    public string Email { get; set; } = "";
}

public class BirthInfo
{
    public int Age { get; set; }
    public string Address { get; set; } = "";
}

public class DestDto
{
    public string Name { get; set; } = "";
    public BirthInfoDto? BirthInfo { get; set; }
    public string ContactInfo { get; set; } = "";
}

public class BirthInfoDto
{
    public int Age { get; set; }
    public string Address { get; set; } = "";
}