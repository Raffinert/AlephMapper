using AlephMapper;

namespace Tests;

public static partial class PersonMapper
{
    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", Generate = AdaptGeneration.MapAndExpression)]
    public static PersonDto MapPerson(Person source) => new()
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName,
        Email = source.Email
    };
}

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
