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
    public static Expression<Func<Employee, EmployeeDto>> MapEmployeeExpression(bool includeEmail) => 
        source => includeEmail
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
    /// This is an auto-generated adapted expression companion for <see cref="MapPersonWithDetail(PersonWithDetail, string)"/>.
    /// </summary>
    public static Expression<Func<EmployeeWithDetail, EmployeeWithDetailDto>> MapEmployeeWithDetailExpression(string userLanguageCode) => 
        source => new EmployeeWithDetailDto
        {
            Id = source.Id,
            Details = source.Details
                .Select(detail => new DetailDto
                {
                    Code = detail.Code,
                    Description = detail.Descriptions
                        .Where(description => description.LanguageCode == userLanguageCode)
                        .Select(description => description.Text)
                        .FirstOrDefault() ?? detail.Code
                })
                .ToList()
        };
}
