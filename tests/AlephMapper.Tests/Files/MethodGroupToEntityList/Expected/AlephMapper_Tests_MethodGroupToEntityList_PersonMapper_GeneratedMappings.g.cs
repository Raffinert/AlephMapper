using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests.MethodGroupToEntityList;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class PersonMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="ToEntity(PersonDto)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<PersonDto, Person>> ToEntityExpression() => 
        dto => new Person
        {
            ContactNumbers = dto.PhoneNumbers.Select(dto => new PhoneNumber
            {
                Number = dto.Number
            }).ToList()
        };
}
