using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;

namespace Tests;

[GeneratedCode("AlephMapper", "0.6.1")]
partial class AdaptUpdateMapper
{
    /// <summary>
    /// This is an auto-generated adapted mapping method for <see cref="MapPerson(PersonUpdateDto, string)"/>.
    /// </summary>
    public static Employee MapEmployee(EmployeeUpdateDto source, string prefix) =>
        new Employee
        {
            Name = prefix + source.Name,
            Email = source.Email
        };

    /// <summary>
    /// This is an auto-generated adapted update method for <see cref="MapPerson(PersonUpdateDto, string)"/>.
    /// </summary>
    public static Employee MapEmployee(EmployeeUpdateDto source, string prefix, Employee dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new Employee();
        dest.Name = prefix + source.Name;
        dest.Email = source.Email;
        return dest;
    }
}
