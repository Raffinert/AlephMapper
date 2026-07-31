[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/banner2-direct.svg)](https://stand-with-ukraine.pp.ua)

## Terms of use<sup>[?](https://github.com/Tyrrrz/.github/blob/master/docs/why-so-political.md)</sup>

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all the following statements:

- You **condemn Russia and its military aggression against Ukraine**
- You **recognize that Russia is an occupant that unlawfully invaded a sovereign state**
- You **support Ukraine's territorial integrity, including its claims over temporarily occupied territories of Crimea and Donbas**
- You **reject false narratives perpetuated by Russian state propaganda**

To learn more about the war and how you can help, [click here](https://stand-with-ukraine.pp.ua). Glory to Ukraine!

# AlephMapper

[![NuGet](https://img.shields.io/nuget/v/AlephMapper.svg)](https://www.nuget.org/packages/AlephMapper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AlephMapper.svg)](https://www.nuget.org/packages/AlephMapper)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Write a mapping once as ordinary C#. Use it in memory, as an EF Core projection, or to update an existing object.**

AlephMapper is a C# source generator for explicit, reusable mappings. It generates companion methods from your expression-bodied mapping code:

- `Expression<Func<TSource, TDestination>>` factories for LINQ providers such as EF Core;
- update-in-place overloads for existing destination instances;
- explicitly requested mappings for structurally compatible type pairs.

There is no runtime mapping configuration and no second projection implementation to keep synchronized.

## Why AlephMapper?

A normal C# mapping is easy to write, call, debug, and refactor:

```csharp
public static PersonDto MapPerson(Person person) => new()
{
    Id = person.Id,
    Name = person.FirstName + " " + person.LastName
};
```

EF Core projections usually require the same logic in an expression tree:

```csharp
public static Expression<Func<Person, PersonDto>> MapPersonExpression() =>
    person => new PersonDto
    {
        Id = person.Id,
        Name = person.FirstName + " " + person.LastName
    };
```

Nested mappings make this duplication worse because ordinary methods cannot be called transparently inside an expression translated by EF Core. AlephMapper expands supported mapping and helper calls at compile time, generating one provider-visible expression from the original C# methods.

Use AlephMapper when you prefer:

- handwritten mappings over convention-based member discovery;
- compile-time generation over runtime mapping configuration;
- ordinary method composition over manual expression-tree composition;
- inspectable generated code and compiler diagnostics.

AlephMapper does not automatically discover mappings between arbitrary types, and the target LINQ provider still determines which generated expressions it can translate.

## Installation

Using the .NET CLI:

```bash
dotnet add package AlephMapper
```

Using `PackageReference`:

```xml
<PackageReference Include="AlephMapper" Version="0.6.1">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

With Central Package Management:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="AlephMapper" Version="0.6.1" />

<!-- Project file -->
<PackageReference Include="AlephMapper">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

When referencing the generator directly from source:

```xml
<ProjectReference Include="..\path\to\AlephMapper.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

`PrivateAssets="all"` prevents AlephMapper from flowing transitively to consumers of your library. `IncludeAssets` makes its analyzer and source-generator assets available during compilation.

## Quick start

Mapping methods must be `static`, expression-bodied, and declared in a `static partial` class.

Add `using AlephMapper;`, then apply `[Expressive]` to a mapping method or its containing class:

```csharp
using AlephMapper;

public static partial class PersonMapper
{
    [Expressive]
    public static PersonDto MapPerson(Employee employee) => new()
    {
        Id = employee.EmployeeId,
        FullName = GetFullName(employee),
        Email = employee.ContactInfo.Email,
        Department = employee.Department.Name
    };

    private static string GetFullName(Employee employee) =>
        employee.FirstName + " " + employee.LastName;
}
```

AlephMapper generates a projection companion and inlines the helper:

```csharp
public static partial class PersonMapper
{
    public static Expression<Func<Employee, PersonDto>> MapPersonExpression() =>
        employee => new PersonDto
        {
            Id = employee.EmployeeId,
            FullName = employee.FirstName + " " + employee.LastName,
            Email = employee.ContactInfo.Email,
            Department = employee.Department.Name
        };
}
```

Use the generated expression in an EF Core query:

```csharp
var people = await dbContext.Employees
    .Select(PersonMapper.MapPersonExpression())
    .ToListAsync();
```

Use the original method in memory:

```csharp
var person = PersonMapper.MapPerson(employee);
```

## Composing mappings and predicates

Supported expression-bodied methods can call other mapping or helper methods. AlephMapper substitutes their arguments and expands their bodies into the generated expression:

```csharp
public static partial class OrderMapper
{
    [Expressive]
    public static OrderDto MapOrder(Order order) => new()
    {
        Id = order.Id,
        Customer = MapCustomer(order.Customer),
        Lines = order.Lines.Select(MapLine).ToList()
    };

    private static CustomerDto MapCustomer(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name
    };

    private static OrderLineDto MapLine(OrderLine line) => new()
    {
        ProductId = line.ProductId,
        Quantity = line.Quantity
    };
}
```

`[Expressive]` is not limited to object projections. A method returning `bool` generates an `Expression<Func<TSource, bool>>`, allowing statically known conditions to be composed as ordinary methods:

```csharp
public static partial class EmployeeConditions
{
    [Expressive]
    public static bool IsEligible(Employee employee) =>
        IsActive(employee) &&
        HasRequiredExperience(employee, 3);

    private static bool IsActive(Employee employee) =>
        employee.IsActive;

    private static bool HasRequiredExperience(
        Employee employee,
        int minimumYears) =>
        employee.YearsOfExperience >= minimumYears;
}
```

```csharp
var employees = await dbContext.Employees
    .Where(EmployeeConditions.IsEligibleExpression())
    .ToListAsync();
```

This complements rather than replaces runtime predicate builders: use AlephMapper when the condition structure is known at compile time, and a dynamic expression API when runtime input determines the number or shape of conditions.

## Context parameters

A mapping may accept values after its source parameter. AlephMapper moves those values to the generated expression factory while keeping a single source parameter in the returned expression:

```csharp
public static partial class EmployeeMapper
{
    [Expressive]
    public static EmployeeDto Map(
        Employee employee,
        int currentYear) => new()
    {
        Id = employee.Id,
        YearsOfExperience = currentYear - employee.StartYear
    };
}
```

The generated signature is:

```csharp
public static Expression<Func<Employee, EmployeeDto>>
    MapExpression(int currentYear) =>
        employee => new EmployeeDto
        {
            Id = employee.Id,
            YearsOfExperience = currentYear - employee.StartYear
        };
```

```csharp
var employees = await dbContext.Employees
    .Select(EmployeeMapper.MapExpression(DateTime.UtcNow.Year))
    .ToListAsync();
```

Helpers may also have multiple parameters. Positional and named arguments are substituted according to the helper's declared parameters.

## Extension-method inlining

AlephMapper supports expression-bodied extension methods declared with the traditional `this` parameter syntax:

```csharp
public static class ProductExtensions
{
    public static string FormatPrice(
        this Product product,
        string prefix) =>
        prefix + product.Price;
}

public static partial class ProductMapper
{
    [Expressive]
    public static ProductDto Map(Product product) => new()
    {
        Name = product.Name,
        Price = product.FormatPrice("$")
    };
}
```

The generated expression contains `"$" + product.Price`, not a call to `FormatPrice`. Extension mapping methods can also be used as LINQ method groups:

```csharp
Addresses = person.Addresses
    .Select(AddressMapper.ToDto)
    .ToList()
```

### Null-safe mapping at the call site

Extension mappings work naturally with null-conditional access. This places one null check around the complete inlined mapping:

```csharp
public static partial class AddressMapper
{
    public static AddressDto ToDto(this Address address) => new()
    {
        Street = address.Street,
        City = address.City,
        Country = address.Country
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class PersonMapper
{
    public static PersonDto ToDto(Person person) => new()
    {
        Name = person.Name,
        Address = person.Address?.ToDto()
    };
}
```

AlephMapper inlines `ToDto()` and generates an expression equivalent to:

```csharp
person => new PersonDto
{
    Name = person.Name,
    Address = person.Address != null
        ? new AddressDto
        {
            Street = person.Address.Street,
            City = person.Address.City,
            Country = person.Address.Country
        }
        : null
}
```

The equivalent regular static call needs a manual condition:

```csharp
Address = person.Address != null
    ? AddressMapper.ToDto(person.Address)
    : null
```

`person.Address?.ToDto()` is both shorter and more explicit about the intended behavior: the mapping is skipped when the receiver is null. The generated expression checks the nullable boundary once instead of requiring a separate check for every mapped member.

This transformation is not applied automatically to regular method calls. `MapAddress(address)` always invokes `MapAddress`, even when `address` is null, whereas `address?.ToDto()` skips the invocation. Treating those forms as equivalent could change program behavior.

Apply `?.` only at genuinely nullable boundaries. Once inside the non-null extension mapping, access the receiver normally. A receiver must be a stable member-access path. Expression generation is skipped with diagnostic `AM0016` when rewriting a receiver such as `GetAddress()?.ToDto()` could evaluate it more than once.

Modern C# extension blocks are not currently supported.

## Null handling

C# null-conditional access (`?.`) is not directly supported in expression trees. Configure its treatment with `NullConditionalRewrite`:

```csharp
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class PersonMapper
{
    public static PersonDto Map(Person person) => new()
    {
        Name = person.Name,
        City = person.Address?.City
    };
}
```

| Policy | Generated behavior |
| --- | --- |
| `None` | Skips expression generation and reports `AM0017` because expression trees do not support null-conditional syntax. |
| `Ignore` | Removes conditional access: `person.Address?.City` becomes `person.Address.City`. |
| `Rewrite` | Emits an explicit check: `person.Address != null ? person.Address.City : null`. |

`Ignore` is the default. Use `Rewrite` when the generated expression must retain the null-safe behavior of the original method.

`None` does not inline through a conditional extension call because doing so would detach the inlined body from `?.` and change its semantics. Expression generation is skipped with `AM0017` instead of emitting uncompilable or behavior-changing code. Use `Rewrite` to produce an expression-compatible explicit null check.

## Updating existing objects

Apply `[Updatable]` to generate an overload that writes mapped properties to an existing destination:

```csharp
public static partial class PersonMapper
{
    [Updatable]
    public static Person Map(PersonUpdateDto source) => new()
    {
        FirstName = source.FirstName,
        LastName = source.LastName,
        Email = source.Email
    };
}
```

Generated shape:

```csharp
public static Person Map(PersonUpdateDto source, Person target)
{
    target.FirstName = source.FirstName;
    target.LastName = source.LastName;
    target.Email = source.Email;
    return target;
}
```

This preserves EF Core's tracked instance:

```csharp
var person = await dbContext.People.FindAsync(id);

PersonMapper.Map(request, target: person);

await dbContext.SaveChangesAsync();
```

Collection properties are skipped by default. Enable their update explicitly:

```csharp
[Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
public static Order Map(OrderRequest source) => new()
{
    Lines = source.Lines.Select(MapLine).ToList()
};
```

Replacing tracked collections can affect relationships and persistence behavior, so opt in deliberately. Update generation for value-type destinations reports `AM0001` because value types do not provide useful update-in-place semantics.

## Adapting a mapping template

`[Adapt]` reuses a mapping body for one explicitly declared source/destination pair. The types need not share a base type or interface; AlephMapper validates the members, constructors, and conversions required by the template.

```csharp
public static partial class PersonMapper
{
    [Adapt(
        typeof(Employee),
        typeof(EmployeeDto),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Map | AdaptGeneration.Expression)]
    public static PersonDto MapPerson(Person source) => new()
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName,
        Email = source.Email
    };
}
```

AlephMapper generates:

```csharp
public static EmployeeDto MapEmployee(Employee source) => new()
{
    Id = source.Id,
    Name = source.FirstName + " " + source.LastName,
    Email = source.Email
};

public static Expression<Func<Employee, EmployeeDto>>
    MapEmployeeExpression() =>
        source => new EmployeeDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName,
            Email = source.Email
        };
```

`Generate` defaults to `AdaptGeneration.Map | AdaptGeneration.Expression`. Combine `Map`, `Expression`, and `Update` as needed.

`Name` is required when expression generation is requested. For map-only or update-only adaptations, omitting it uses the template method's name. Additional template parameters are preserved in generated signatures.

Adaptation is explicit: AlephMapper does not scan for compatible types. Invalid adaptations report diagnostics `AM0005` through `AM0015`. See the [`[Adapt]` technical guide](docs/Adapt-Attribute.md) for validation rules and examples.

## Generated API

| Attribute | Generated member |
| --- | --- |
| `[Expressive]` | `<MethodName>Expression(...)` returning `Expression<Func<TSource, TDestination>>` |
| `[Updatable]` | An overload with a final destination parameter named `target` |
| `[Adapt]` | The requested adapted map, expression, and/or update members |

Attributes can be applied to individual methods. `[Expressive]` and `[Updatable]` can also be applied to the containing class.

AlephMapper is best suited to object initializers, predicates, constructor calls, member access, conversions, LINQ operations, and small expression-bodied methods that it can inline. Not every valid C# construct can be represented in an expression tree, and not every expression-tree operation can be translated by every query provider.

## Inspecting generated code

To emit generated files from a consuming project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>
    $(BaseIntermediateOutputPath)Generated
  </CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Inspect generated code to confirm method inlining, null handling, adapted members, and the exact expression supplied to EF Core.

## Troubleshooting

### A generated method is missing

Confirm that the mapper class is `static partial`, the method is `static` and expression-bodied, and the appropriate attribute is applied to the method or class. For `[Adapt]`, check the explicit source/destination types and provide `Name` when generating an expression.

### EF Core cannot translate a generated expression

AlephMapper generates an expression tree; EF Core and its database provider translate it. Inspect the generated expression and the provider exception for unsupported operations.

### A helper or extension method was not inlined

The method must be visible in the current compilation and use a supported expression-bodied shape. Extension methods must use the traditional `this` parameter syntax. Circular helper calls report `AM0002` or `AM0003` and skip the affected generation.

### `?.` causes a `NullReferenceException`

The default `Ignore` policy removes null-conditional access from generated expressions. Select `NullConditionalRewrite.Rewrite` to generate explicit null checks.

## Comparison

| Tool | Primary approach | AlephMapper's focus |
| --- | --- | --- |
| AutoMapper | Runtime configuration and conventions | Explicit C# mappings with compile-time companions |
| Mapster | Configuration/conventions with runtime and generated options | Handwritten mapping methods as the source of truth |
| Mapperly | Compile-time mapping generation from declarations and conventions | Expressions and updates derived from an existing implementation |
| EntityFrameworkCore.Projectables | Projectable members expanded for EF Core | Complete mappings, predicates, updates, and explicit adaptation |
| Expressionify | Expression expansion | Mapping-oriented companion generation |
| LINQKit | Runtime/query-time expression composition and expansion | Compile-time expansion of statically known composition |

## Examples

- [Sample application](examples/SampleApp)
- [`[Adapt]` technical guide](docs/Adapt-Attribute.md)
- [Integration tests](tests/AlephMapper.IntegrationTests)

## Contributing

Contributions are welcome.

1. Fork the repository.
2. Create a feature branch.
3. Make the change.
4. Add or update tests.
5. Run the test suite.
6. Open a pull request.

## License

AlephMapper is licensed under the [MIT License](LICENSE).

## Acknowledgments

AlephMapper was inspired by [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) and [Expressionify](https://github.com/ClaveConsulting/Expressionify). Thanks to all [contributors](https://github.com/Raffinert/AlephMapper/graphs/contributors).

## Related projects

- [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables)
- [Expressionify](https://github.com/ClaveConsulting/Expressionify)
- [LINQKit](https://github.com/scottksmith95/LINQKit)
- [NeinLinq](https://github.com/axelheer/nein-linq)
- [Mapperly](https://github.com/riok/mapperly)
- [AutoMapper](https://automapper.org/)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [Facet](https://github.com/Tim-Maes/Facet)
