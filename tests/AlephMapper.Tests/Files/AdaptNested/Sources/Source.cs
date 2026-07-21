using AlephMapper;

namespace Tests;

public static partial class PersonMapper
{
    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", Generate = AdaptGeneration.Map | AdaptGeneration.Expression)]
    public static PersonDto MapPerson(Person source, bool includeEmail) => includeEmail
        ? new PersonDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName,
            Email = source.Email
        }
        : new PersonDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName,
            Email = string.Empty
        };

    [Adapt(typeof(EmployeeWithDetail), typeof(EmployeeWithDetailDto), Name = "MapEmployeeWithDetail", Generate = AdaptGeneration.Expression)]
    public static PersonWithDetailDto MapPersonWithDetail(PersonWithDetail source, string userLanguageCode) => new()
    {
        Id = source.Id,
        Details = source.Details.Select(detail => MapDetail(detail, userLanguageCode)).ToList()
    };

    public static DetailDto MapDetail(Detail detail, string userLanguageCode) => new()
    {
        Code = detail.Code,
        Description = detail.Descriptions
            .Where(description => description.LanguageCode == userLanguageCode)
            .Select(description => description.Text)
            .FirstOrDefault() ?? detail.Code
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

public class PersonWithDetail
{
    public int Id { get; set; }
    public List<Detail> Details { get; set; } = [];
}

public class PersonWithDetailDto
{
    public int Id { get; set; }
    public List<DetailDto> Details { get; set; } = [];
}

public class EmployeeWithDetail
{
    public int Id { get; set; }
    public List<Detail> Details { get; set; } = [];
}

public class EmployeeWithDetailDto
{
    public int Id { get; set; }
    public List<DetailDto> Details { get; set; } = [];
}

public class Detail
{
    public string Code { get; set; } = string.Empty;
    public List<DetailDescription> Descriptions { get; set; } = [];
}

public class DetailDescription
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class DetailDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
