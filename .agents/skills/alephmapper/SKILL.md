---
name: alephmapper
description: Use AlephMapper in C#/.NET applications when creating handwritten source-generated mappings, EF Core projection companions, update-in-place overloads, explicit structural adaptations, or diagnosing generated mapping and null-handling behavior. Covers Projectable, Updatable, Adapt, generated APIs, and provider translation limits.
license: MIT
metadata:
  author: Raffinert
  project: https://github.com/Raffinert/AlephMapper
---

# AlephMapper — application integration

AlephMapper uses an ordinary, handwritten C# mapping method as the source of truth and generates companion members at **compile time**. Use it when the consuming application benefits from a reusable mapping, a provider-visible expression, an update overload, or a deliberately requested adaptation. Do not introduce it solely to replace a simple one-off assignment.

## Workflow

1. Inspect the consuming application's installed AlephMapper version, C# compiler, models, nullable policy, query provider, and desired mapping direction. Keep the application's real domain terminology.
2. Choose an explicit mapping method and generated companions, rather than assuming convention-based mapping or automatic reverse mapping.
3. Declare the mapper in a `static partial class` and import `AlephMapper`. Classic mapping methods must be `static`, expression-bodied, and have a source parameter. C# 14 extension blocks also support instance methods and expression-bodied getter-only properties (Roslyn 5.0+). Instance methods can generate projections, updates, and explicit adaptations; properties remain projection-only.
4. Select the needed attribute(s): `[Projectable]` for an expression companion, `[Updatable]` for an update overload, or method-level `[Adapt]` for an explicitly declared compatible source/destination pair.
5. Choose null-conditional behavior deliberately for expressions. The default `NullConditionalRewrite.Ignore` **removes** `?.` in generated expressions; use `Rewrite` to preserve its semantics. The ordinary handwritten method retains its C# behavior.
6. Build the consumer project, inspect generated members and diagnostics, and test the original mapping separately from its generated expression or update overload. For `IQueryable`, execute against the real database provider.

## Generated API: do not guess names or signatures

| Input | Generated companion / rule |
|---|---|
| `[Projectable] public static TDest Map(TSource source) => ...;` | `MapExpression()` returns `Expression<Func<TSource, TDest>>`. Extra parameters after the source become parameters of the **expression factory**, not additional lambda inputs. |
| `[Updatable] public static TDest Map(TSource source) => ...;` | An overload `Map(source, dest)` updates an existing destination. The generated destination parameter is named `dest`. |
| `[Projectable]` on a supported C# 14 extension method or getter-only expression property | A static `<MemberName>Expression(...)` factory in the outer `static partial` class, with the extension receiver as the expression input. |
| `[Updatable]` on a C# 14 instance extension method returning an object | A static `<MemberName>(receiver, ..., dest)` overload that updates `dest`. The receiver is the source parameter. |
| `[Adapt]` on a C# 14 instance extension method | Explicit static map/expression/update companions named by the adaptation, with receiver type replaced by the selected source type. |
| `[Adapt(typeof(OtherSource), typeof(OtherDest), Name = "MapOther")]` | Explicitly generates the requested members for that pair; default flags are `Map | Expression`. `Name` is required when generating an expression; `Update` is opt-in. |

For classic mappings, `[Projectable]` and `[Updatable]` may be applied to individual methods or their containing class. For C# 14 extension members, `[Projectable]` supports instance methods and getter-only expression properties. `[Updatable]` and `[Adapt]` are supported on **instance extension methods only**, not computed properties; applying them to an outer class does not implicitly enable them on extension members. `[Adapt]` is method-only; it does not discover arbitrary pairs, automatically reverse a mapping, or support adaptation of generic mapping methods.

Minimal projection:

~~~csharp
using AlephMapper;

public static partial class CustomerMapper
{
    [Projectable]
    public static CustomerDto ToDto(Customer customer) => new()
    {
        Id = customer.Id,
        DisplayName = customer.FirstName + " " + customer.LastName
    };
}

var one = CustomerMapper.ToDto(customer); // ordinary C# mapping
var rows = await db.Customers
    .Select(CustomerMapper.ToDtoExpression()) // generated companion
    .ToListAsync();
~~~

For further recipes, read [projections.md](references/projections.md), [updates-and-null.md](references/updates-and-null.md), and [adaptation-and-diagnostics.md](references/adaptation-and-diagnostics.md) only when relevant.

## Query and generator boundaries

- AlephMapper expands supported expression-bodied mapping and helper calls **at compilation**. It is not a runtime mapping registry, an EF query interceptor, or a general-purpose runtime expression composer.
- In EF queries, pass the generated expression to `Select` or `Where`. Do not assume `Select(x => CustomerMapper.ToDto(x))` is translated merely because `ToDtoExpression()` exists.
- A generated expression is only provider-visible C#; EF Core and the selected provider still determine translation. Test server-side behavior instead of relying on an in-memory test.
- For null-safe extension mappings, use `receiver?.ToDto()` with `NullConditionalRewrite.Rewrite` only for stable nullable member-access receivers. A regular `MapAddress(address)` call has different null semantics.
- Supported C# 14 extension blocks require Roslyn 5.0+; generic, block-bodied and other unsupported member shapes report `AM0021`. See the [extension compatibility matrix](https://github.com/Raffinert/AlephMapper/blob/main/docs/extension-members-compatibility-matrix.md).
- No silent implicit map discovery or unrequested two-way mapping. Avoid invented AlephMapper APIs and preserve the consuming application's domain model.

## Installation and verification

The documented package is `AlephMapper` 0.8.3, packaged as a compile-time analyzer/source generator targeting `netstandard2.0`. It supports Roslyn 4.8 and later through versioned analyzer assets; C# 14 extension members require Roslyn 5.0 or later. Verify requirements for the installed version. A `PackageReference` used in a library should configure generator assets appropriately (see the repository README).

Inspect compiler diagnostics (including `AM0019` for switch patterns and `AM0021`–`AM0023` for extension members and declaration/signature issues) and, when debugging, emit compiler-generated files. Verify generated signatures rather than inventing them. Do not treat a generated code compilation failure as an EF translation problem.

Canonical documentation: https://github.com/Raffinert/AlephMapper (README, `docs/Adapt-Attribute.md`, integration tests).
