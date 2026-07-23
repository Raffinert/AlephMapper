using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace Tests;

[GeneratedCode("AlephMapper", "0.6.0")]
partial class PersonMapper
{
    /// <summary>
    /// This is an auto-generated adapted mapping method for <see cref="MapPerson(Person)"/>.
    /// </summary>
    public static EmployeeDto MapEmployee(Employee source) =>
        new EmployeeDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName,
            Email = source.Email
        };

    /// <summary>
    /// This is an auto-generated adapted expression companion for <see cref="MapPerson(Person)"/>.
    /// </summary>
    public static Expression<Func<Employee, EmployeeDto>> MapEmployeeExpression() => 
        source => new EmployeeDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName,
            Email = source.Email
        };
}
