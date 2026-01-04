using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class SimpleObjectMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToDto(SimpleObject)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<SimpleObject, SimpleDto>> MapToDtoExpression() => 
        so => new SimpleDto
        {
            Attributes = so.Attributes.Select(attr => attr.Name).ToList() ?? new List<string>()
        };
}
