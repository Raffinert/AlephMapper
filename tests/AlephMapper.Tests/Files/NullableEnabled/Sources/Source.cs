#nullable enable
using AlephMapper;

namespace Tests;

[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class NullableEnabledMapper
{
    public static string? GetName(Person person) => person?.Name;
}

public class Person
{
    public string? Name { get; set; }
}
