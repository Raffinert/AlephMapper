using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class NestedExt_PersonMapper_Ignore
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(NestedExt_Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<NestedExt_Person, NestedExt_PersonDto>> ToDtoExpression() => 
        p => new NestedExt_PersonDto
        {
            HomeAddress = p.Friend.HomeAddress.ToDto()
        };
}
