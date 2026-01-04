using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class TechDebtPersonMapperNone
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(TechDebtTestPerson)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are preserved as-is in the expression tree.
    /// </para>
    /// </remarks>
    public static Expression<Func<TechDebtTestPerson, TechDebtTestPersonDto>> ToDtoExpression() => 
        person => new TechDebtTestPersonDto
        {
            Name = person.Name,
            AddressStr = person.Address.FormattedAddress ?? "No Address"
        };
}
