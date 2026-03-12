using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParameterInlining;

[GeneratedCode("AlephMapper", "0.5.5")]
partial class UpdatableMultiParamMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(Person, int)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int, PersonDto>> ToDtoExpression() => 
        (person, currentYear) => new PersonDto
        {
            FullName = person.First + " " + person.Last,
            BirthYear = currentYear - person.Age
        };

    /// <summary>
    /// This is an auto-generated update method for <see cref="ToDto(Person, int)"/>.
    /// </summary>
    /// <param name="person">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="currentYear"/>
    /// <param name="dest">The destination object to update. If null, the new instance is created.</param>
    /// <returns>The updated destination object for method chaining, or the new destination instance if either parameter is null.</returns>
    public static PersonDto ToDto(Person person, int currentYear, PersonDto dest)
    {
        if (person == null) return dest;
        if (dest == null)
            dest = new PersonDto();
        dest.FullName = person.First + " " + person.Last;
        dest.BirthYear = currentYear - person.Age;
        return dest;
    }
}
