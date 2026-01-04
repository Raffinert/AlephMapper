using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class RewriteMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetAddress(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, string>> GetAddressExpression() => 
        dto => (dto.BirthInfo != null
            ? (dto.BirthInfo.Address) 
            : (string)null) ?? "Unknown";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="HasAddress(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, bool>> HasAddressExpression() => 
        source => (source.BirthInfo != null
            ? (source.BirthInfo.Address) 
            : (string)null) != null;
}
