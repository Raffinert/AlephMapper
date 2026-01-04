using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class IgnoreMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetAddress(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, string>> GetAddressExpression() => 
        source => source.BirthInfo.Address ?? "Unknown";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="HasAddress(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, bool>> HasAddressExpression() => 
        source => source.BirthInfo.Address != null;
}
