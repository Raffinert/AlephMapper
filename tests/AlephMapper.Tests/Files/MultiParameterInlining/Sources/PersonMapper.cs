using AlephMapper;

namespace AlephMapper.Tests.MultiParameterInlining;

public class Person
{
    public string First { get; set; } = string.Empty;
    public string Last { get; set; } = string.Empty;
}

public class PersonDto
{
    public string FullName { get; set; } = string.Empty;
}

public static partial class PersonMapper
{
    [Expressive]
    public static PersonDto ToDto(Person person) => new()
    {
        FullName = Combine(person.First, person.Last)
    };

    public static string Combine(string first, string last) => first + " " + last;
}
