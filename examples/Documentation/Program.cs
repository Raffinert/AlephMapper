using AlephMapper;
using System.Linq;

namespace Documentation;

public sealed class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int BirthYear { get; set; }
}

public sealed class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public sealed class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int BirthYear { get; set; }
}

public sealed class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public static partial class PersonMapper
{
    // docs-example:start projection
    [Projectable]
    public static PersonDto ToDto(Person person, int year) => new()
    {
        Id = person.Id,
        Name = FullName(person),
        Age = year - person.BirthYear
    };

    private static string FullName(Person person) =>
        person.FirstName + " " + person.LastName;
    // docs-example:end projection

    // docs-example:start predicate
    [Projectable]
    public static bool IsAdult(Person person, int year) =>
        year - person.BirthYear >= 18;
    // docs-example:end predicate

    // docs-example:start update
    [Updatable]
    public static PersonDto UpdateTemplate(Person person) => new()
    {
        Id = person.Id,
        Name = person.FirstName + " " + person.LastName
    };
    // docs-example:end update

    // docs-example:start adaptation
    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "ToEmployeeDto")]
    public static PersonDto AdaptTemplate(Person person) => new()
    {
        Id = person.Id,
        Name = person.FirstName + " " + person.LastName
    };
    // docs-example:end adaptation
}

public static partial class PersonExtensions
{
    // docs-example:start extension
    extension(Person person)
    {
        [Projectable]
        public string DisplayName => person.FirstName + " " + person.LastName;
    }
    // docs-example:end extension
}

public static class Program
{
    public static void Main()
    {
        var person = new Person { Id = 1, FirstName = "Ada", LastName = "Lovelace", BirthYear = 2000 };
        var people = new[] { person }.AsQueryable();

        // docs-example:start calls
        var direct = PersonMapper.ToDto(person, 2026);
        var projected = people.Select(PersonMapper.ToDtoExpression(2026)).Single();
        var adults = people.Where(PersonMapper.IsAdultExpression(2026)).ToList();
        var destination = new PersonDto();
        PersonMapper.UpdateTemplate(person, dest: destination);
        var employee = new Employee { Id = 2, FirstName = "Grace", LastName = "Hopper", BirthYear = 1906 };
        var adapted = PersonMapper.ToEmployeeDto(employee);
        var adaptedExpression = PersonMapper.ToEmployeeDtoExpression().Compile()(employee);
        var name = people.Select(PersonExtensions.DisplayNameExpression()).Single();
        // docs-example:end calls

        if (direct.Name != "Ada Lovelace" || projected.Age != 26 || adults.Count != 1 ||
            !ReferenceEquals(PersonMapper.UpdateTemplate(person, destination), destination) ||
            destination.Name != "Ada Lovelace" || adapted.Name != "Grace Hopper" ||
            adaptedExpression.Name != "Grace Hopper" || name != "Ada Lovelace")
        {
            throw new InvalidOperationException("Documentation example failed.");
        }

        var created = PersonMapper.UpdateTemplate(person, dest: null!);
        if (created.Name != "Ada Lovelace" ||
            !ReferenceEquals(PersonMapper.UpdateTemplate(null!, destination), destination))
        {
            throw new InvalidOperationException("Update null behavior failed.");
        }

        Console.WriteLine("Documentation examples passed.");
    }
}
