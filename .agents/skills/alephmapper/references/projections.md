# Projectable mapping and EF Core projections

Use these patterns only when they match the installed AlephMapper version. The original mapping remains callable C#; the generated `<MethodName>Expression` is the projection factory.

## One mapping, two consumption modes

~~~csharp
using AlephMapper;

public static partial class CustomerMapper
{
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static CustomerDto ToDto(Customer customer, int currentYear) => new()
    {
        Id = customer.Id,
        DisplayName = FormatName(customer),
        YearsActive = currentYear - customer.StartYear,
        City = customer.Address?.City
    };

    private static string FormatName(Customer customer) =>
        customer.FirstName + " " + customer.LastName;
}

var one = CustomerMapper.ToDto(customer, currentYear); // executes handwritten method
var rows = await db.Customers
    .Select(CustomerMapper.ToDtoExpression(currentYear)) // generated factory
    .ToListAsync();
~~~

`ToDtoExpression(currentYear)` returns an `Expression<Func<Customer, CustomerDto>>`. The first mapping parameter is the source of the lambda; subsequent mapping parameters are bound through the generated factory. Supported expression-bodied helper invocations are inlined into the expression; this does not turn arbitrary CLR calls into translatable SQL. Keep mapped members and results consistent with real consumer model types.

## Predicates are projectable too

~~~csharp
public static partial class CustomerRules
{
    [Projectable]
    public static bool IsActive(Customer customer) => customer.IsActive;
}

var active = await db.Customers
    .Where(CustomerRules.IsActiveExpression())
    .ToListAsync();
~~~

This is useful for statically known business rules. Do not use it as a substitute for a dynamic runtime predicate builder when the condition's structure must change based on runtime input.

## Inlining and collection mappings

- Supported static expression-bodied helper and traditional `this` extension method calls can be inlined.
- `source.Addresses.Select(AddressMapper.ToDto).ToList()` is an available composition pattern when the mapper helper is suitable for inlining; inspect generated output and execute a provider test.
- Never assume arbitrary methods, modern extension blocks, recursion, or unsupported C# syntax can be represented in an expression tree.
- Do not rely on `Include` to repair a projection: verify navigation translation against the chosen EF Core provider.
- Validate observed rows and meaningful SQL behavior with SQLite/SQL Server tests, not just `IEnumerable` tests.
