namespace AlephMapper.Tests;

// Projection mappers for EF Core integration tests

[Expressive(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public static partial class EfCoreMapper
{
    public static PersonSummaryDto GetPersonComplex(Person p) => new PersonSummaryDto
    {
        Id = p.Id,
        Name = GetPersonName(p),
        Email = GetPersonEmail(p),
        Age = GetPersonAge(p),
        BirthPlace = GetBirthPlace(p),
        BirthAddress = GetBirthAddress(p),
        HasBirthInfo = HasBirthInfo(p),
        IsAdult = IsAdult(p),
        AddressCount = GetAddressCount(p),
        OrderCount = GetOrderCount(p),
        HasActiveAddress = HasActiveAddress(p),
        PersonCategory = GetPersonCategory(p),
        Summary = GetPersonSummary(p)
    };

    // Simple property mapping
    public static string GetPersonName(Person person) => person.Name;

    public static string GetPersonEmail(Person person) => person.Email;

    // Null conditional operator with navigation property
    public static int? GetPersonAge(Person person) => person.BirthInfo?.Age;

    public static bool IsOlderThan30(Person person) => GetPersonAge(person) > 30;

    public static string GetBirthPlace(Person person) => person.BirthInfo?.BirthPlace ?? "Unknown";

    public static string GetBirthAddress(Person person) => person.BirthInfo?.Address ?? "Not specified";

    // Complex navigation and calculations
    public static bool HasBirthInfo(Person person) => person.BirthInfo != null;

    public static bool IsAdult(Person person) => person.BirthInfo?.Age >= 18;

    public static bool BornInUkraine(Person person) =>
        person.BirthInfo?.Address != null && person.BirthInfo.Address.Contains("Ukraine");

    // Collection-based projections
    public static int GetAddressCount(Person person) => person.Addresses.Count;

    public static int GetOrderCount(Person person) => person.Orders.Count;

    public static bool HasActiveAddress(Person person) =>
        person.Addresses.Any(a => a.IsActive);

    public static bool HasCompletedOrders(Person person) =>
        person.Orders.Any(o => o.IsCompleted);

    // More complex projections with multiple null conditionals
    public static string GetFirstActiveAddressCity(Person person) =>
        person.Addresses.FirstOrDefault(a => a.IsActive)?.City ?? "No active address";

    public static decimal GetTotalOrderAmount(Person person) =>
        person.Orders.Where(o => o.IsCompleted).Sum(o => o.Amount);

    public static string GetLatestOrderNumber(Person person) =>
        person.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault()?.OrderNumber ?? "No orders";

    // Updated to include age information in the summary
    public static string GetPersonSummary(Person person) =>
        person.BirthInfo != null ?
            person.Name + " (" + person.BirthInfo.Age + " years old) from " +
            (person.BirthInfo.BirthPlace ?? "Unknown") :
            person.Name + " (unknown age) from Unknown";

    public static bool LivesInSamePlaceAsBorn(Person person) =>
        person.BirthInfo?.BirthPlace != null &&
        person.Addresses.Any(a => a.IsActive && a.City == person.BirthInfo.BirthPlace);

    // Simplified conditional logic using if statements
    public static string GetPersonCategory(Person person)
    => person.BirthInfo == null ? "Unknown Age"
       : person.BirthInfo.Age < 18 ? "Minor"
       : person.BirthInfo.Age < 65 ? "Adult"
       : "Senior";

    public static bool IsVipCustomer(Person person) =>
        person.Orders.Where(o => o.IsCompleted).Sum(o => o.Amount) >= 1000m;
}

// Mapper with Ignore policy for comparison
[Expressive(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public static partial class EfCoreIgnoreMapper
{
    public static string GetPersonName(Person person) => person.Name;

    public static string GetPersonEmail(Person person) => person.Email;

    // These will ignore null conditional operators and may throw NullReferenceException
    public static int? GetPersonAge(Person person) => person.BirthInfo?.Age;

    public static string GetBirthPlace(Person person) => person.BirthInfo?.BirthPlace ?? "Unknown";
}

// DTO classes for projection results
public class PersonSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int? Age { get; set; }
    public string BirthPlace { get; set; } = "";
    public string BirthAddress { get; set; } = "";
    public bool HasBirthInfo { get; set; }
    public bool IsAdult { get; set; }
    public int AddressCount { get; set; }
    public int OrderCount { get; set; }
    public bool HasActiveAddress { get; set; }
    public string PersonCategory { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class PersonOrderDto
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = "";
    public decimal TotalOrderAmount { get; set; }
    public string LatestOrderNumber { get; set; } = "";
    public bool HasCompletedOrders { get; set; }
    public bool IsVipCustomer { get; set; }
}

public class PersonAddressDto
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = "";
    public string FirstActiveAddressCity { get; set; } = "";
    public bool LivesInSamePlaceAsBorn { get; set; }
    public bool BornInUkraine { get; set; }
}