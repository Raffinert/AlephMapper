using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace Tests;

[GeneratedCode("AlephMapper", "0.5.2")]
partial class NullableDisabledMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetName(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetNameExpression() => 
        person => (person != null
            ? (person.Name) 
            : (string)null);
}
