using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace Tests;

[GeneratedCode("AlephMapper", "0.5.7")]
partial class PersonMapper
{
    /// <summary>
    /// This is an auto-generated adapted mapping method for <see cref="MapPerson(Person, bool)"/>.
    /// </summary>
    public static EmployeeDto MapEmployee(Employee source, bool includeEmail) =>
        includeEmail
            ? new EmployeeDto
            {
                Id = source.Id,
                Name = source.FirstName + " " + source.LastName,
                Email = source.Email
            }
            : new EmployeeDto
            {
                Id = source.Id,
                Name = source.FirstName + " " + source.LastName,
                Email = string.Empty
            };

    /// <summary>
    /// This is an auto-generated adapted expression companion for <see cref="MapPerson(Person, bool)"/>.
    /// </summary>
    public static Expression<Func<Employee, bool, EmployeeDto>> MapEmployeeExpression() => 
        (source, includeEmail) => includeEmail
            ? new EmployeeDto
            {
                Id = source.Id,
                Name = source.FirstName + " " + source.LastName,
                Email = source.Email
            }
            : new EmployeeDto
            {
                Id = source.Id,
                Name = source.FirstName + " " + source.LastName,
                Email = string.Empty
            };
}
