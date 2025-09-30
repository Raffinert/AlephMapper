using AlephMapper;

namespace AlephMapper.Tests;

[Expressive(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public static partial class DebugRewriteMapper
{
    // Simple null conditional - should generate: dto => dto.BirthInfo != null ? dto.BirthInfo.Address : null
    public static string GetAddressSimple(SourceDto dto) => dto.BirthInfo?.Address;
}

[Expressive(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public static partial class DebugIgnoreMapper  
{
    // Simple null conditional - should generate: dto => dto.BirthInfo.Address
    public static string GetAddressSimple(SourceDto dto) => dto.BirthInfo?.Address;
}