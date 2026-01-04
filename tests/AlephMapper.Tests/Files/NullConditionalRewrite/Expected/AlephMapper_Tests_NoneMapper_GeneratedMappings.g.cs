using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class NoneMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetName(SourceDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are preserved as-is in the expression tree.
    /// </para>
    /// </remarks>
    public static Expression<Func<SourceDto, string>> GetNameExpression() => 
        source => source.Name;
}
