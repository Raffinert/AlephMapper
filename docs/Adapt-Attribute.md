# `[Adapt]` attribute

`[Adapt]` reuses a manually written mapping method as a compile-time template for one or more explicitly declared source and destination type pairs. For each declaration, AlephMapper emits a new mapping method, an expression companion, or both.

It is intended for types with the same *used* mapping shape. It does not discover types by convention, scan the project for compatible pairs, or use runtime reflection.

## Contents

- [When to use it](#when-to-use-it)
- [Attribute declarations](#attribute-declarations)
- [Basic usage](#basic-usage)
- [Generation modes and naming](#generation-modes-and-naming)
- [Compatibility rules](#compatibility-rules)
- [Generated output](#generated-output)
- [Diagnostics](#diagnostics)
- [Implementation architecture](#implementation-architecture)
- [Known limits](#known-limits)
- [Testing](#testing)

## When to use it

Use `[Adapt]` when a mapping body should be shared by explicitly named type pairs whose members have compatible names and types.

For example, `Person` and `Employee` can both contain the fields used by a template, while `PersonDto` and `EmployeeDto` can both receive the initializer assignments. A single `Person -> PersonDto` template can therefore produce a separate `Employee -> EmployeeDto` API.

Use `[Projectable]` when the generated expression is for the original method's declared signature. Use `[Adapt]` when the generated API should use a different, explicit source and/or destination type. The two attributes can be applied to the same template method.

## Attribute declarations

The generator emits these `internal` attribute definitions into the consuming compilation during post-initialization, where mapper declarations in that compilation can use them. The source definitions live in [`source/Attributes.cs`](../source/Attributes.cs). The generated mapping companions are `public` members of the mapper class.

```csharp
[Flags]
internal enum AdaptGeneration
{
    Map = 1,
    Expression = 2,
    Update = 4
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class AdaptAttribute : Attribute
{
    public AdaptAttribute(Type sourceType, Type destinationType);

    public Type SourceType { get; }
    public Type DestinationType { get; }
    public string Name { get; set; }
    public AdaptGeneration Generate { get; set; } = AdaptGeneration.Map | AdaptGeneration.Expression;
    public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
}
```

`SourceType` and `DestinationType` are required `typeof(...)` constructor arguments. Each `[Adapt]` is independent, so a method may be adapted to multiple pairs.

`NullConditionalRewrite` applies while helper methods are inlined for that adaptation. It uses the same policies as `[Projectable]`:

| Value | Effect |
| --- | --- |
| `None` | Leaves null-conditionals unchanged. |
| `Ignore` | Treats a null-conditional access as ordinary member access. This is the default. |
| `Rewrite` | Rewrites it to an explicit null check. |

## Basic usage

The template must be an expression-bodied static method in a `static partial` class and must have at least one parameter. The first parameter is the adapted source; the return type is the template destination. Any remaining parameters are retained unchanged.

```csharp
using AlephMapper;

public static partial class PersonMapper
{
    [Adapt(
        typeof(Employee),
        typeof(EmployeeDto),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Map | AdaptGeneration.Expression)]
    public static PersonDto MapPerson(Person source, int currentYear) => new()
    {
        Id = source.Id,
        DisplayName = source.FirstName + " " + source.LastName,
        YearsActive = currentYear - source.StartYear
    };
}
```

The attribute explicitly says that `Employee` replaces `Person` as the first parameter type and `EmployeeDto` replaces `PersonDto` as the mapping result. `currentYear` remains an `int` parameter.

The original method remains callable and is not rewritten:

```csharp
PersonDto person = PersonMapper.MapPerson(personSource, currentYear);
```

## Generation modes and naming

`Generate` is a flags enum:

| Setting | Members emitted |
| --- | --- |
| `AdaptGeneration.Map` | A regular mapping method. |
| `AdaptGeneration.Expression` | An `Expression<Func<...>>` factory. |
| `AdaptGeneration.Update` | An overload that updates an existing destination instance. |
| `AdaptGeneration.Map | AdaptGeneration.Expression` | Both members; this is the default. |

`Name` is the generated base name.

```csharp
[Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
```

This produces `MapEmployee(...)` and `MapEmployeeExpression()` when the default mode is used.

To generate an adapted update overload, include `Update`:

```csharp
[Adapt(
    typeof(EmployeeUpdateDto),
    typeof(Employee),
    Name = "MapEmployee",
    Generate = AdaptGeneration.Map | AdaptGeneration.Update)]
```

This produces `MapEmployee(EmployeeUpdateDto source)` and `MapEmployee(EmployeeUpdateDto source, Employee dest)`; the chosen flags omit the expression factory. Any additional template parameters appear before `dest`. Update generation uses the template's `CollectionProperties` policy and has the same value-type and circular-reference safeguards as `[Updatable]`.

An expression factory takes any additional template parameters after the source parameter, so an expression-generating adaptation must supply `Name`. Otherwise the generator reports `AM0011` and skips that adaptation. A map-only adaptation may omit `Name`; in that case it uses the template method name and is emitted as an overload if the adapted first-parameter type makes the signature distinct.

The generator rejects both conflicts with existing members and conflicts between generated adaptations. It never appends numeric suffixes or silently changes an API name.

## Compatibility rules

Compatibility is based on the members used by the template rather than on a full type-to-type comparison.

```csharp
public static PersonDto Map(Person source) => new()
{
    Id = source.Id,
    City = source.Address.City
};
```

For an adaptation of this template:

- The adapted source must expose readable instance members `Id`, `Address`, and `Address.City`; inherited properties and fields are considered.
- The adapted destination must expose writable instance members `Id` and `City`.
- When an initializer right-hand side is directly a source member path, its adapted source member type must be implicitly convertible to the adapted destination member type.
- Each destination construction must have a constructor that can accept the original construction's arguments under the generator's constructor compatibility rules. Optional arguments and `params` arrays are supported.

Members not referenced by the mapping body are irrelevant. The adapted types may contain extra members and do not need to be related by inheritance.

The resulting generated source must still be valid C#. In particular, any method calls, operators, casts, conditional branches, or helper bodies used by the template must also make sense for the substituted types.

## Generated output

From the previous example, AlephMapper emits the following shape (formatting and fully-qualified type names vary with the compilation):

```csharp
public static EmployeeDto MapEmployee(Employee source, int currentYear) =>
    new EmployeeDto
    {
        Id = source.Id,
        DisplayName = source.FirstName + " " + source.LastName,
        YearsActive = currentYear - source.StartYear
    };

public static Expression<Func<Employee, EmployeeDto>> MapEmployeeExpression(int currentYear) =>
    source => new EmployeeDto
    {
        Id = source.Id,
        DisplayName = source.FirstName + " " + source.LastName,
        YearsActive = currentYear - source.StartYear
    };
```

The template's first-parameter *name* is retained, so the body does not need a source-variable rewrite. Its *type* changes because the generated method declares that parameter with the adapted source type. The destination construction is rewritten to the adapted destination type.

Helper methods that can be inlined are expanded before emission. This makes the expression companion useful in LINQ providers that require a self-contained expression tree.

Generated members are written into the same namespace and partial mapper declaration. For nested or generic mapper types, the emitter recreates the containing partial-type hierarchy and generic type parameter lists. Every enclosing type of a nested mapper must be a `partial class`; containing structs and records are unsupported. Otherwise [AM0021](diagnostics.md#am0021) reports the declaration and companions are omitted.

## Diagnostics

All adaptation diagnostics use the `AlephMapper` category. Unless otherwise noted, they are errors and prevent only the invalid adaptation from being generated.

| ID | Severity | Meaning |
| --- | --- | --- |
| `AM0005` | Error | The adapted pair is the same as the template's original source and destination pair. |
| `AM0006` | Error | A member path used from the source cannot be resolved as a readable instance property or field on the adapted source. |
| `AM0007` | Error | A destination initializer member is absent or not writable on the adapted destination. |
| `AM0008` | Error | A directly assigned adapted source member is not implicitly convertible to the adapted destination member. |
| `AM0009` | Error | A generated map or expression name/signature conflicts with an existing or generated member. |
| `AM0010` | Warning | The template does not contain a recognized construction of its declared destination type when adapting to a different result type. |
| `AM0011` | Error | Expression generation was requested without `Name`. |
| `AM0012` | Error | The template declares the same adapted source/destination pair more than once. |
| `AM0013` | Warning | A circular helper reference was found while inlining the template. |
| `AM0014` | Error | The source or destination is an open generic type, or contains a type parameter. |
| `AM0015` | Error | Roslyn rebinding of the proposed adapted member fails compilation validation. |

For the full current catalog, including generation consequences, see [diagnostics](diagnostics.md).

## Implementation architecture

The current implementation is summarized in the [generator architecture overview](architecture.md). `MappingAnalysisFactory` reads the attribute into `AdaptationAnalysis`; `AdaptationMemberEmitter` validates the explicit pair, rewrites the destination body, reserves signatures through `GeneratedMemberRegistry`, and emits requested members. Roslyn rebinding validates the proposed generated member. The [source files](../source/Generation/Emitters/AdaptationMemberEmitter.cs) and [diagnostic catalog](diagnostics.md) are the authority for detailed failure behavior.

## Known limits

- Adaptation is method-only. Class-level `[Adapt]` is not supported.
- A template must have an expression body and at least one parameter; the first parameter is always the source being adapted.
- Open generic adapted types and adapted types containing type parameters are rejected.
- Validation first checks structural paths and assignments, then Roslyn rebinding checks the proposed generated member. Unsupported source forms can still be rejected, and provider translation remains a separate concern.
- Only properties and fields participate in source-path and destination-initializer validation. Indexers and arbitrary method-call shapes do not receive dedicated adaptation checks.
- Destination-member validation is limited to object-initializer assignments on constructions recognized as the template return type.
- An adaptation whose source and destination are both identical to the template pair is rejected rather than generating a duplicate API.

## Testing

Adaptation coverage is in [`tests/AlephMapper.Tests`](../tests/AlephMapper.Tests):

- Baseline cases `Files/AdaptBoth` and `Files/AdaptNested` verify generated source for map-plus-expression and nested mapper output.
- `SourceGeneratorTests` compiles generated output for implicit object creation, generic/nested mappers, optional and `params` constructors, and source instance method calls.
- The same test suite asserts each adaptation diagnostic (`AM0005`–`AM0015`) and an incompatible direct member assignment.
- The SampleApp includes [`AdaptExampleMapper.cs`](../examples/SampleApp/Mappers/AdaptExampleMapper.cs), which adapts an applicant template to contractor types and invokes both generated members.

To inspect the emitted code in a consumer project, enable compiler-generated-file output:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```
