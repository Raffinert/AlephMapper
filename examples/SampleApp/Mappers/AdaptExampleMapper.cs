using AlephMapper;
using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

/// <summary>
/// Demonstrates [Adapt]: one mapping template reused for an explicitly declared
/// different source/destination pair. Unlike [Projectable], [Adapt] is not for the
/// exact method signature types; it structurally substitutes the template source
/// and destination with the explicit types from the attribute.
/// </summary>
public static partial class AdaptExampleMapper
{
    [Adapt(
        typeof(ContractorRecord),
        typeof(ContractorBriefDto),
        Name = "MapContractorBrief",
        Generate = AdaptGeneration.Map | AdaptGeneration.Expression)]
    public static ApplicantBriefDto MapApplicantBrief(ApplicantProfile source, int currentYear, string tenant) => new()
    {
        Id = source.Id,
        DisplayName = FormatDisplayName(source.Name.First, source.Name.Last, source.Title),
        ContactLine = FormatContact(source.Contact.Email, source.Contact.Phone),
        Location = FormatLocation(source.WorkAddress.City, source.WorkAddress.State, source.WorkAddress.Country),
        YearsActive = YearsSince(source.StartYear, currentYear),
        RoutingKey = BuildRoutingKey(tenant, source.Id),
        Score = CalculateScore(source.Rating, source.CompletedProjects)
    };

    private static string FormatDisplayName(string first, string last, string title)
        => title + " " + first + " " + last;

    private static string FormatContact(string email, string phone)
        => email + " / " + phone;

    private static string FormatLocation(string city, string state, string country)
        => city + ", " + state + ", " + country;

    private static int YearsSince(int fromYear, int currentYear)
        => currentYear - fromYear;

    private static string BuildRoutingKey(string tenant, int id)
        => tenant + "-" + id;

    private static decimal CalculateScore(decimal rating, int completedProjects)
        => rating * 10m + completedProjects;
}
