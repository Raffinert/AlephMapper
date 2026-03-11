using AlephMapper;
using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

/// <summary>
/// Demonstrates multi-parameter helper method inlining.
/// The generator will inline calls like FormatName(first, last) directly
/// into the generated expression tree and updatable method body.
/// </summary>
public static partial class EmployeeMapper
{
    [Expressive]
    [Updatable]
    public static EmployeeSummaryDto ToSummary(Employee emp, int year) => new()
    {
        Id = emp.EmployeeId,
        FullName = FormatName(emp.FirstName, emp.LastName),
        DisplayName = FormatDisplayName(emp.Title, emp.FirstName, emp.LastName),
        ContactInfo = FormatContact(emp.FirstName, emp.LastName, emp.Email),
        Age = YearsSince(emp.BirthYear, year),
        YearsOfService = YearsSince(emp.StartYear, year),
        Location = FormatLocation(emp.City, emp.State, emp.Country),
        DepartmentTitle = CombineWithSeparator(emp.Department, emp.Title, " - "),
        TotalCompensation = CalculateCompensation(emp.BaseSalary, emp.BonusPercent)
    };

    // --- Multi-parameter helpers that the generator will inline ---

    // Two-param: simple string concatenation
    private static string FormatName(string first, string last)
        => first + " " + last;

    // Three-param: builds a display name like "Sr. Engineer John Doe"
    private static string FormatDisplayName(string title, string first, string last)
        => title + " " + first + " " + last;

    // Three-param: builds contact info like "John Doe <john@example.com>"
    private static string FormatContact(string first, string last, string email)
        => first + " " + last + " <" + email + ">";

    // Two-param numeric: subtraction
    private static int YearsSince(int fromYear, int toYear)
        => toYear - fromYear;

    // Three-param: location formatting
    private static string FormatLocation(string city, string state, string country)
        => city + ", " + state + ", " + country;

    // Three-param with a separator argument
    private static string CombineWithSeparator(string left, string right, string separator)
        => left + separator + right;

    // Two-param decimal: salary + bonus calculation
    private static decimal CalculateCompensation(decimal baseSalary, decimal bonusPercent)
        => baseSalary + baseSalary * bonusPercent / 100m;
}
