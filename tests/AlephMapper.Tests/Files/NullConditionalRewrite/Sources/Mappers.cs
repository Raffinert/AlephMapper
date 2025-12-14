using System.CodeDom.Compiler;

namespace AlephMapper.Tests;

// Test mapper with Ignore policy (now default, but being explicit)
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class IgnoreMapper
{
    public static string GetAddress(SourceDto source) => source.BirthInfo?.Address ?? "Unknown";

    public static bool HasAddress(SourceDto source) => source.BirthInfo?.Address != null;
}

// Test mapper with Rewrite policy
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class RewriteMapper
{
    public static string GetAddress(SourceDto dto) => dto.BirthInfo?.Address ?? "Unknown";

    public static bool HasAddress(SourceDto source) => source.BirthInfo?.Address != null;
}

// Test mapper with None policy (should fail with null conditional operators)
[Expressive(NullConditionalRewrite = NullConditionalRewrite.None)]
public static partial class NoneMapper
{
    // This method should work because it doesn't use null conditional operators
    public static string GetName(SourceDto source) => source.Name;
}