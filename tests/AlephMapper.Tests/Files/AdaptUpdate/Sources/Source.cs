using AlephMapper;

namespace Tests;

public static partial class AdaptUpdateMapper
{
    [Adapt(
        typeof(EmployeeUpdateDto),
        typeof(Employee),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Map | AdaptGeneration.Update)]
    public static Person MapPerson(PersonUpdateDto source, string prefix) => new()
    {
        Name = prefix + source.Name,
        Email = source.Email
    };
}

public class PersonUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class EmployeeUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class Person
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class Employee
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
