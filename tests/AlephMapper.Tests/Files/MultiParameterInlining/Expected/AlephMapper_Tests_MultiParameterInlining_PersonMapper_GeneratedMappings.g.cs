using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParameterInlining;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class PersonMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, PersonDto>> ToDtoExpression() => 
        person => new PersonDto
        {
            FullName = person.First + " " + person.Last
        };
}
