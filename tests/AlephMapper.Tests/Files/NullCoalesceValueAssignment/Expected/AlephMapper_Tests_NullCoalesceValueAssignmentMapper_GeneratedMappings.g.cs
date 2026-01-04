using AgileObjects.ReadableExpressions;
using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class NullCoalesceValueAssignmentMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Map(ValueSource)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<ValueSource, ValueDest>> MapExpression() => 
        s => new ValueDest
        {
            Must = s.Maybe ?? 42
        };
}
