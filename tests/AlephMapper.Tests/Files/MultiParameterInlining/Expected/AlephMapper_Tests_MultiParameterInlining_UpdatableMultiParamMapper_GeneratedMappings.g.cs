using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParameterInlining;

[GeneratedCode("AlephMapper", "0.5.3")]
partial class UpdatableMultiParamMapper
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
            FullName = person.First + " " + person.Last,
            BirthYear = 2026 - person.Age
        };

    /// <summary>
    /// Updates an existing instance of <see cref="PersonDto"/> with values from the source object.
    /// </summary>
    /// <param name="person">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static PersonDto ToDto(Person person, PersonDto dest)
    {
        if (person == null) return dest;
        if (dest == null)
            dest = new PersonDto();
        dest.FullName = person.First + " " + person.Last;
        dest.BirthYear = 2026 - person.Age;
        return dest;
    }
}
