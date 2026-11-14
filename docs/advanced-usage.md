# Advanced AlephMapper usage

For a short introduction, start with the [README](../README.md). AlephMapper creates companion methods at compilation from explicit, expression-bodied mappings. These examples use illustrative domain models.

## Generated companions

| Input | Generated API |
| --- | --- |
| `[Projectable]` on a classic mapping | `<MethodName>Expression(...)` returning an `Expression<Func<TSource, TResult>>` |
| `[Updatable]` | An overload with a final destination parameter named `dest` |
| `[Adapt]` | Only the explicitly requested adapted map/expression/update members |

Classic mapping methods must be `static`, expression-bodied, and contained in a `static partial` class. `[Projectable]` and `[Updatable]` can be applied to a classic method or its containing class. `[Adapt]` is method-level.

Generated expressions do not imply that every expression node is translatable by EF Core or its provider. Pass the generated factory's output to `Select`/`Where`, rather than assuming an ordinary call such as `Select(x => Mapper.Map(x))` will be translated.

## Context parameters

Parameters after the source parameter become arguments of the generated expression factory:

```csharp
public static partial class EmployeeMapper
{
    [Projectable]
    public static EmployeeDto ToDto(Employee employee, int currentYear) => new()
    {
        Id = employee.Id,
        YearsOfExperience = currentYear - employee.StartYear
    };
}

var rows = await db.Employees
    .Select(EmployeeMapper.ToDtoExpression(DateTime.UtcNow.Year))
    .ToListAsync();
```

The expression is still a single-source-parameter `Expression<Func<Employee, EmployeeDto>>`.

## Nested and extension-method inlining

Supported expression-bodied mapping and helper methods can call other supported methods. AlephMapper inlines their bodies at compile time, including extension methods and supported method groups such as `items.Select(MapLine)`.

```csharp
public static class ProductExtensions
{
    public static string FormatPrice(this Product product, string prefix) =>
        prefix + product.Price;
}

public static partial class ProductMapper
{
    [Projectable]
    public static ProductDto ToDto(Product product) => new()
    {
        Name = product.Name,
        FormattedPrice = product.FormatPrice("$")
    };
}
```

The generated projection substitutes the extension body instead of leaving an opaque `FormatPrice` call.

C# 14 extension blocks support instance methods and expression-bodied getter-only instance properties with the documented restrictions. Supported instance methods may generate projection, update, and adaptation companions, while properties are projection-only. See the [extension compatibility matrix](extension-members-compatibility-matrix.md).

## Null-conditional expressions

The default `NullConditionalRewrite.Ignore` **removes** `?.` from generated expressions. The original handwritten C# method retains ordinary null-conditional semantics. Choose a policy deliberately when a nullable path is significant.

| Policy | Generated behavior |
| --- | --- |
| `Ignore` (default) | Removes `?.`: `person.Address?.City` becomes `person.Address.City` |
| `Rewrite` | Generates an explicit null check and conditional result |
| `None` | Does not generate the expression for unsupported null-conditional syntax; reports `AM0017` |

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

Null-safe extension mappings can place a conditional at the call site:

```csharp
Address = person.Address?.ToDto()
```

Unlike an ordinary static method call, this skips invocation for a null receiver. For safe rewriting, the receiver must be a stable member-access path; e.g. `GetAddress()?.ToDto()` can be rejected with `AM0016` to avoid evaluating the receiver more than once. `None` deliberately does not silently inline through an unsupported conditional extension call.

## Updating existing objects

`[Updatable]` generates a method that updates the existing destination instance, useful for tracked entities:

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

PersonMapper.Map(request, dest: person);
```

Collection properties are skipped by default. Updating them is opt-in:

```csharp
[Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]
public static Order Map(OrderRequest source) => new()
{
    Lines = source.Lines.Select(MapLine).ToList()
};
```

`Skip` leaves a mapped collection property on the existing destination untouched, including its reference and contents. `Update` emits the initializer's assignment; it does not merge by key or mutate the existing list in place. If the right-hand side is `source.Lines`, the destination receives that same collection reference. If it is `source.Lines.Select(MapLine).ToList()`, the destination receives a new list and new mapped child objects. An empty mapped source produces an empty replacement list. A guarded nullable source expression can assign `null` without dereferencing it. Properties absent from the initializer retain their destination values. A null source object returns the existing destination without assignments.

The [collection contract tests](../tests/AlephMapper.IntegrationTests/CollectionContractTests.cs) verify these cases. In their specific required SQLite one-to-many relationship, replacing a tracked employee's address list marks the old child deleted and the new child added after change detection; save/reload persists the replacement. Other relationship configurations can behave differently, so inspect tracking states and persistence in your model. Update-in-place generation for non-nullable value-type destinations is not supported (`AM0001`).

## Switch expressions and side effects

Supported C# `switch` patterns are lowered to conditional expressions. The input can be evaluated **more than once** after lowering, unlike a native C# switch. Inlined helper arguments can also be duplicated, skipped, or reordered. Use stable, side-effect-free inputs and arguments in projection code.

Unsupported or non-exhaustive switch patterns report `AM0019`. Check the [switch-pattern support matrix](switch-parity-matrix.md) for precise coverage. The resulting tree must still be translatable by the selected LINQ provider.

For tested computed projections, predicates, null navigation, and nested mapping on EF Core SQLite 10.0.14, see the [SQL translation evidence](sqlite-translation-evidence.md). This evidence is scoped to those shapes and provider version.

## Inspect generated code

Enable generated files in the consuming project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>
    $(BaseIntermediateOutputPath)Generated
  </CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Inspect the generated source for inlined helpers, adapted members, null handling, and the tree passed to EF Core. The generated file follows the consuming project's nullable configuration; it uses `#nullable restore` rather than preserving source-local nullable directives.

## Troubleshooting

For every `AMxxxx` ID, default severity, cause, fix, and missing-companion consequence, use the [diagnostic catalog](diagnostics.md). For exact companion signatures, see the [generated API reference](generated-api-reference.md).

- **Missing generated method:** check `static partial` mapper declaration, expression-bodied mapping, and attribute placement. For C# 14 extension members, verify Roslyn 5.0+ and the supported member forms (`AM0020`).
- **EF Core cannot translate a projection:** inspect the generated expression and the provider exception. A generated C# expression tree does not guarantee SQL translation.
- **Helper was not inlined:** use a supported, visible, expression-bodied method; cyclic helper calls are rejected (`AM0002`/`AM0003`).
- **Null reference after `?.`:** the default is `Ignore`; opt into `NullConditionalRewrite.Rewrite` where needed.
- **Adaptation failed:** check used members, constructors, destination writeability, and `Name` for expression generation. See the [`[Adapt]` guide](Adapt-Attribute.md).
- **Generated member collision or invalid mapper class:** check diagnostics `AM0021`/`AM0022`.

See [installation and compiler compatibility](installation-and-compatibility.md) for package setup and [integration tests](../tests/AlephMapper.IntegrationTests) for executable behavior.
