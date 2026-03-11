using AlephMapper;

namespace AlephMapper.Tests.MultiParameterInlining;

public class Person
{
    public string First { get; set; } = string.Empty;
    public string Last { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class PersonDto
{
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int BirthYear { get; set; }
}

/// <summary>
/// Tests basic two-parameter helper method inlining.
/// Combine(person.First, person.Last) should become person.First + " " + person.Last
/// </summary>
public static partial class PersonMapper
{
    [Expressive]
    public static PersonDto ToDto(Person person) => new()
    {
        FullName = Combine(person.First, person.Last),
        Address = FormatAddress(person.Street, person.City, person.Zip),
        Description = Describe(person.First, person.Last, person.Age),
        BirthYear = YearFromAge(person.Age, 2026)
    };

    public static string Combine(string first, string last) => first + " " + last;

    public static string FormatAddress(string street, string city, string zip) =>
        street + ", " + city + " " + zip;

    public static string Describe(string first, string last, int age) =>
        first + " " + last + " (age " + age + ")";

    public static int YearFromAge(int age, int currentYear) => currentYear - age;
}

/// <summary>
/// Tests named argument mapping in multi-parameter helper inlining.
/// Combine(last: person.Last, first: person.First) should still produce person.First + " " + person.Last
/// </summary>
public static partial class NamedArgMapper
{
    [Expressive]
    public static PersonDto ToDto(Person person) => new()
    {
        FullName = Combine(last: person.Last, first: person.First)
    };

    public static string Combine(string first, string last) => first + " " + last;
}

/// <summary>
/// Tests nested multi-parameter helper inlining: a helper calls another multi-param helper.
/// DescribeWithAge calls Combine internally, both should be inlined.
/// </summary>
public static partial class NestedMultiParamMapper
{
    [Expressive]
    public static PersonDto ToDto(Person person) => new()
    {
        FullName = DescribeWithAge(person.First, person.Last, person.Age)
    };

    public static string DescribeWithAge(string first, string last, int age) =>
        Combine(first, last) + " (age " + age + ")";

    public static string Combine(string a, string b) => a + " " + b;
}


/// <summary>
/// Tests that a multi-parameter [Expressive] mapping method itself
/// generates the correct Expression with multiple Func type arguments.
/// </summary>
public static partial class MultiParamExpressiveMapper
{
    [Expressive]
    public static PersonDto Map(Person person, int currentYear) => new()
    {
        FullName = person.First + " " + person.Last,
        BirthYear = currentYear - person.Age
    };
}

/// <summary>
/// Tests multi-parameter helper combined with [Updatable] to ensure
/// the updatable method signature includes all parameters.
/// </summary>
public static partial class UpdatableMultiParamMapper
{
    [Expressive]
    [Updatable]
    public static PersonDto ToDto(Person person, int currentYear) => new()
    {
        FullName = Combine(person.First, person.Last),
        BirthYear = YearFromAge(person.Age, currentYear)
    };

    public static string Combine(string first, string last) => first + " " + last;

    public static int YearFromAge(int age, int currentYear) => currentYear - age;
}
