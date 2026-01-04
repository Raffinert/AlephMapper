using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class EfCoreIgnoreMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonName(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonNameExpression() => 
        person => person.Name;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonEmail(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonEmailExpression() => 
        person => person.Email;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonAge(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int?>> GetPersonAgeExpression() => 
        person => person.BirthInfo.Age;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetBirthPlace(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetBirthPlaceExpression() => 
        person => person.BirthInfo.BirthPlace ?? "Unknown";
}
