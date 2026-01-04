using AgileObjects.ReadableExpressions;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class TechDebtPersonMapperRewrite
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(TechDebtTestPerson)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<TechDebtTestPerson, TechDebtTestPersonDto>> ToDtoExpression() => 
        person => new TechDebtTestPersonDto
        {
            Name = person.Name,
            AddressStr = (person.Address != null
                ? (person.Address.FormattedAddress) 
                : (string)null) ?? "No Address"
        };
}
