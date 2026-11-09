[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/banner2-direct.svg)](https://stand-with-ukraine.pp.ua)

# AlephMapper

**Write a mapping once as ordinary C#. Use it in memory, as an EF Core projection, or to update an existing object.**

AlephMapper is a compile-time C# source generator. It turns your handwritten, expression-bodied mapping methods into reusable `Expression<Func<...>>` factories, update-in-place overloads, and explicitly requested mappings for compatible types. Nested mapping and helper calls can be inlined into generated expressions—without a runtime mapping configuration or a second implementation to maintain.

[![NuGet](https://img.shields.io/nuget/v/AlephMapper.svg)](https://www.nuget.org/packages/AlephMapper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AlephMapper.svg)](https://www.nuget.org/packages/AlephMapper)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Quick start

```shell
dotnet add package AlephMapper
```

Add `[Projectable]` to a `static`, expression-bodied method in a `static partial` class:

```csharp
using AlephMapper;

public static partial class EmployeeMapper
{
    [Projectable]
    public static EmployeeDto ToDto(Employee employee) => new()
    {
        Id = employee.Id,
        Name = FullName(employee)
    };

    private static string FullName(Employee employee) =>
        employee.FirstName + " " + employee.LastName;
}
```

Use the original mapping in memory **or** the generated expression in EF Core:

```csharp
var dto = EmployeeMapper.ToDto(employee);

var employees = await db.Employees
    .Select(EmployeeMapper.ToDtoExpression())
    .ToListAsync();
```

AlephMapper inlines the `FullName` helper into the generated expression, so EF Core receives the mapping logic rather than a call to an arbitrary C# method. The database provider still determines which expression nodes it can translate.

## One mapping, several capabilities

| Attribute | What it generates |
| --- | --- |
| `[Projectable]` | An `<MethodName>Expression(...)` factory for LINQ and EF Core. |
| `[Updatable]` | An overload that updates an existing destination object. |
| `[Adapt]` | Explicit map, projection, and optionally update methods for another compatible source/destination pair. |

### Update an existing object

```csharp
public static partial class PersonMapper
{
    [Updatable]
    public static Person Map(PersonUpdateDto source) => new()
    {
        FirstName = source.FirstName,
        LastName = source.LastName
    };
}

var person = await db.People.FindAsync(id);
PersonMapper.Map(request, dest: person!); // Generated overload.
await db.SaveChangesAsync();
```

This reuses the handwritten assignments without replacing the tracked entity. Collection updates are opt-in; see [advanced usage](docs/advanced-usage.md).

### Adapt a mapping to compatible types

```csharp
public static partial class PersonMapper
{
    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "ToEmployeeDto")]
    public static PersonDto ToPersonDto(Person person) => new()
    {
        Id = person.Id,
        Name = person.Name
    };
}

var employeeDto = PersonMapper.ToEmployeeDto(employee);
var query = db.Employees.Select(PersonMapper.ToEmployeeDtoExpression());
```

`[Adapt]` generates only the explicitly requested compatible pairs; it does not discover mappings by convention. See the [adaptation guide](docs/Adapt-Attribute.md).

## Compose mappings and predicates

Supported expression-bodied helpers and nested mappings can be inlined at compile time:

```csharp
public static partial class OrderMapper
{
    [Projectable]
    public static OrderDto ToDto(Order order) => new()
    {
        Id = order.Id,
        Lines = order.Lines.Select(ToLineDto).ToList()
    };

    private static OrderLineDto ToLineDto(OrderLine line) => new()
    {
        ProductId = line.ProductId,
        Quantity = line.Quantity
    };
}

var orders = await db.Orders
    .Select(OrderMapper.ToDtoExpression())
    .ToListAsync();
```

A `[Projectable]` method returning `bool` also generates an `Expression<Func<T, bool>>` factory for `Where`. AlephMapper expands **statically known** composition during compilation; for dynamic runtime composition, use an expression-building library such as [Raffinert.Expressions](https://github.com/Raffinert/Expressions).

## Modern C# support

AlephMapper supports compatible C# `switch` expressions and C# 14 instance extension methods and getter-only expression properties. For example:

```csharp
public static partial class OrderExtensions
{
    extension(Order order)
    {
        [Projectable]
        public decimal AmountWithTax => order.Amount * 1.25m;
    }
}

var amounts = db.Orders
    .Select(OrderExtensions.AmountWithTaxExpression());
```

C# 14 extension methods support projections, updates, and adaptations. Extension properties support projections only.

`switch` lowering can re-evaluate an input or inlined arguments; use side-effect-free expressions. Not every pattern or generated expression is supported by every provider. Check the [extension-member compatibility matrix](docs/extension-members-compatibility-matrix.md) and [switch-pattern support matrix](docs/switch-parity-matrix.md).

## Null handling

**Important:** `NullConditionalRewrite.Ignore` is the default for generated expressions, so it removes `?.`. To preserve null-conditional behavior in projections, opt into rewriting:

```csharp
[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class PersonMapper
{
    public static PersonDto ToDto(Person person) => new()
    {
        City = person.Address?.City
    };
}
```

This preserves the null check in the generated expression. The original mapping method continues to follow ordinary C# semantics. See [advanced usage](docs/advanced-usage.md) for the null policies and their limits.

## AI agent skill

Use the portable skill to help coding agents apply the correct generated APIs and expansion rules:

```shell
npx skills add Raffinert/AlephMapper --skill alephmapper
```

The skill is stored in [`.agents/skills/alephmapper/`](.agents/skills/alephmapper/).

## Documentation

- [Installation, compiler compatibility, and package references](docs/installation-and-compatibility.md)
- [Advanced usage, null handling, and troubleshooting](docs/advanced-usage.md)
- [`[Adapt]` reference](docs/Adapt-Attribute.md)
- [C# 14 extension-member compatibility](docs/extension-members-compatibility-matrix.md)
- [Switch-pattern support and evaluation semantics](docs/switch-parity-matrix.md)
- [Sample application](examples/SampleApp)

AlephMapper is [MIT licensed](LICENSE). Thanks to the [contributors](https://github.com/Raffinert/AlephMapper/graphs/contributors). See also our [statement on Russia's war against Ukraine](docs/terms-of-use.md). [Feedback and issues](https://github.com/Raffinert/AlephMapper/issues) are welcome.
