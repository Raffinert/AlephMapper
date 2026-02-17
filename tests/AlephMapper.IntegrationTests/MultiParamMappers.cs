namespace AlephMapper.IntegrationTests;

// ──────────────────────────────────────────────────────────────────
// 1. Expressive mapper with multi-parameter helper inlining
// ──────────────────────────────────────────────────────────────────
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class MultiParamEmployeeMapper
{
    // Two-parameter helper: concatenate first + last
    public static string FormatName(string first, string last) =>
        first + " " + last;

    // Three-parameter helper: build a formatted address string
    public static string FormatAddress(string street, string city, string country) =>
        street + ", " + city + ", " + country;

    // Mixed-type two-parameter helper: arithmetic
    public static int YearsSince(int startYear, int currentYear) =>
        currentYear - startYear;

    // Nested multi-param: calls FormatName internally
    public static string FormatNameWithEmail(string first, string last, string email) =>
        FormatName(first, last) + " <" + email + ">";

    // Single-param helper to ensure mixing single + multi works
    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    // ── Expression mapping that uses all the above helpers ──
    [Expressive]
    public static EmployeeDto MapToDto(Employee employee) => new()
    {
        Id = employee.Id,
        FullName = FormatName(employee.FirstName, employee.LastName),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive
    };

    // Mapping that exercises three-parameter helper
    [Expressive]
    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee)
    };

    // Mapping that exercises nested multi-param helper
    [Expressive]
    public static EmployeeDto MapToDtoWithEmail(Employee employee) => new()
    {
        Id = employee.Id,
        FullName = FormatNameWithEmail(employee.FirstName, employee.LastName, employee.Email),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive
    };
}

// ──────────────────────────────────────────────────────────────────
// 2. Named-argument mapper — arguments passed out of order
// ──────────────────────────────────────────────────────────────────
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class NamedArgEmployeeMapper
{
    public static string FormatName(string first, string last) =>
        first + " " + last;

    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    [Expressive]
    public static EmployeeDto MapToDto(Employee employee) => new()
    {
        Id = employee.Id,
        // Named arguments in reversed order — should still inline correctly
        FullName = FormatName(last: employee.LastName, first: employee.FirstName),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive
    };
}

// ──────────────────────────────────────────────────────────────────
// 3. Updatable mapper with multi-param helpers
//    (exercises the BinaryExpressionSyntax spacing fix)
// ──────────────────────────────────────────────────────────────────
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class MultiParamUpdatableMapper
{
    public static string FormatName(string first, string last) =>
        first + " " + last;

    public static int YearsSince(int startYear, int currentYear) =>
        currentYear - startYear;

    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    [Updatable]
    public static EmployeeDto MapToDto(Employee employee) => new()
    {
        Id = employee.Id,
        FullName = FormatName(employee.FirstName, employee.LastName),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive
    };
}

// ──────────────────────────────────────────────────────────────────
// 4. Multi-parameter [Expressive] method itself
//    Generates Expression<Func<Employee, int, EmployeeDto>>
// ──────────────────────────────────────────────────────────────────
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class MultiParamExpressiveMapper
{
    public static string FormatName(string first, string last) =>
        first + " " + last;

    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    // The [Expressive] method ITSELF takes two parameters
    [Expressive]
    public static EmployeeDto MapWithYear(Employee employee, int currentYear) => new()
    {
        Id = employee.Id,
        FullName = FormatName(employee.FirstName, employee.LastName),
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee),
        IsActive = employee.IsActive,
        YearsOfExperience = currentYear - 2020 // simple arithmetic with the extra param
    };
}
