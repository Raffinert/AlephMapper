namespace AlephMapper.IntegrationTests;

// ──────────────────────────────────────────────────────────────────
// 1. Projectable mapper with multi-parameter helper inlining
// ──────────────────────────────────────────────────────────────────
[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class MultiParamEmployeeMapper
{
    // Two-parameter helper: concatenate first + last
    public static string FormatName(string first, string last, string separator) =>
        first + separator + last;

    // Three-parameter helper: build a formatted address string
    public static string FormatAddress(string street, string city, string country) =>
        street + ", " + city + ", " + country;

    // Mixed-type two-parameter helper: arithmetic
    public static int YearsSince(int startYear, int currentYear) =>
        currentYear - startYear;

    // Nested multi-param: calls FormatName internally
    public static string FormatNameWithEmail(string first, string last, string email) =>
        FormatName(first, last, " ") + " <" + email + ">";

    // Single-param helper to ensure mixing single + multi works
    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    // ── Expression mapping that uses all the above helpers ──
    [Projectable]
    public static EmployeeDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        FullName = FormatName(e.FirstName, e.LastName, " "),
        Email = e.Email,
        DepartmentName = GetDepartmentName(e),
        IsActive = e.IsActive
    };

    // Mapping that exercises three-parameter helper
    [Projectable]
    public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Email = employee.Email,
        DepartmentName = GetDepartmentName(employee)
    };

    // Mapping that exercises nested multi-param helper
    [Projectable]
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
[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class NamedArgEmployeeMapper
{
    public static string FormatName(string first, string last) =>
        first + " " + last;

    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    [Projectable]
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
[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
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
// 4. Multi-parameter [Projectable] method itself
//    Generates Expression<Func<Employee, EmployeeDto>> MapWithYearExpression(int currentYear)
// ──────────────────────────────────────────────────────────────────
[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class MultiParamProjectableMapper
{
    public static string FormatName(string first, string last) =>
        first + " " + last;

    public static string GetDepartmentName(Employee employee) =>
        employee.Department?.Name ?? "Unassigned";

    // The [Projectable] method ITSELF takes two parameters
    [Projectable]
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
