using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParameterInlining;

[GeneratedCode("AlephMapper", "0.5.5")]
partial class MultiParamExpressiveMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="Map(Person, int)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int, PersonDto>> MapExpression() => 
        (person, currentYear) => new PersonDto
        {
            FullName = person.First + " " + person.Last,
            BirthYear = currentYear - person.Age
        };
}
