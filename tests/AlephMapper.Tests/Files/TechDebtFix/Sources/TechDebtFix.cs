using AgileObjects.ReadableExpressions;

namespace AlephMapper.Tests;

// Tests specifically designed to exercise the tech debt scenarios identified in:
// - InliningResolver.InvocationRewriter.cs lines 79-81 (ParseExpression with string.Join)
// - InliningResolver.NullConditionalRewriter.cs line 45 (ParseExpression for patching expressions)

// Models for testing tech debt scenarios
public class TechDebtTestAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string FormattedAddress => $"{Street}, {City}";
}

public class TechDebtTestPerson
{
    public string Name { get; set; } = string.Empty;
    public TechDebtTestAddress? Address { get; set; }
}

public class TechDebtTestPersonDto
{
    public string Name { get; set; } = string.Empty;
    public string AddressStr { get; set; } = string.Empty;
}

// Extension method mapper that should trigger the tech debt
public static partial class TechDebtAddressMapper
{
    [Expressive]
    public static string FormatAddress(this TechDebtTestAddress address) => 
        address.FormattedAddress;
}

// Mapper with nested conditional access that triggers ParseExpression tech debt
[Expressive(NullConditionalRewrite = NullConditionalRewrite.None)]
public static partial class TechDebtPersonMapperNone
{
    [Expressive] 
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        // This should trigger the conditional access expressions stack issue (lines 79-81 in InvocationRewriter)
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class TechDebtPersonMapperRewrite
{
    [Expressive]
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        // This should trigger both tech debt scenarios:
        // 1. Extension method inlining with conditional access (lines 79-81 in InvocationRewriter) 
        // 2. Null conditional rewriter patching (line 45 in NullConditionalRewriter)
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class TechDebtPersonMapperIgnore
{
    [Expressive]
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}