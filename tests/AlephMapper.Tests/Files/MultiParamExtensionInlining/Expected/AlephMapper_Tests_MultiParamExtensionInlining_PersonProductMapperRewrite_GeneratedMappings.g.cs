using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace AlephMapper.Tests.MultiParamExtensionInlining;

[GeneratedCode("AlephMapper", "0.5.3")]
partial class PersonProductMapperRewrite
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToDto(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, PersonProductDto>> ToDtoExpression() => 
        person => new PersonProductDto
        {
            Name = person.Name,
            FavoritePrice = (person.FavoriteProduct != null
                ? ("$" + person.FavoriteProduct.Price) 
                : (string)null)
        };
}
