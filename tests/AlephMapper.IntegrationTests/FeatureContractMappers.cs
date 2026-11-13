using AlephMapper;

namespace AlephMapper.IntegrationTests;

public sealed class CollectionContractChild
{
    public int Value { get; set; }
}

public sealed class CollectionContractSource
{
    public string Name { get; set; } = "";
    public List<CollectionContractChild>? Children { get; set; }
}

public sealed class CollectionContractDestination
{
    public string Name { get; set; } = "";
    public string Unmapped { get; set; } = "";
    public List<CollectionContractChild>? Children { get; set; }
}

public static partial class CollectionContractMapper
{
    [Updatable]
    public static CollectionContractDestination Skip(CollectionContractSource source) => new()
    {
        Name = source.Name,
        Children = source.Children
    };

    [Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
    public static CollectionContractDestination ReplaceDirect(CollectionContractSource source) => new()
    {
        Name = source.Name,
        Children = source.Children
    };

    [Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
    public static CollectionContractDestination ReplaceMapped(CollectionContractSource source) => new()
    {
        Name = source.Name,
        Children = source.Children == null
            ? null
            : source.Children.Select(child => new CollectionContractChild { Value = child.Value }).ToList()
    };
}

public static partial class TrackedCollectionContractMapper
{
    [Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
    public static Employee ReplaceAddresses(Employee source) => new()
    {
        Addresses = source.Addresses.Select(address => new EmployeeAddress
        {
            Street = address.Street,
            City = address.City,
            Country = address.Country
        }).ToList()
    };
}

public static partial class SqlContractMapper
{
    [Projectable]
    public static string FullName(Employee employee) =>
        employee.FirstName + " " + employee.LastName;

    [Projectable]
    public static bool ActiveJohn(Employee employee) =>
        employee.IsActive && employee.FirstName == "John";

    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static string DepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";
}
