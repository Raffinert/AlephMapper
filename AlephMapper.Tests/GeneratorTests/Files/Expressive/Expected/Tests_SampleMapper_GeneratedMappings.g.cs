using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace Tests;

[GeneratedCode("AlephMapper", "0.4.4")]
partial class SampleMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ProjectName(SampleSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SampleSource, string>> ProjectNameExpression() => 
        source => source.Name;
}
