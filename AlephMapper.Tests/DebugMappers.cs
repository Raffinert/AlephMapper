namespace AlephMapper.Tests;

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class DebugRewriteMapper
{
    // Simple null conditional - should generate: dto => dto.BirthInfo != null ? dto.BirthInfo.Address : null
    public static string GetAddressSimple(SourceDto dto) => dto.BirthInfo?.Address;
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class DebugIgnoreMapper  
{
    // Simple null conditional - should generate: dto => dto.BirthInfo.Address
    public static string GetAddressSimple(SourceDto dto) => dto.BirthInfo?.Address;
}