# `[Adapt]` attribute

`[Adapt]` reuses a manually written mapping method as a compile-time template for one or more explicitly declared source and destination type pairs. For each declaration, AlephMapper emits a new mapping method, an expression companion, or both.

It is intended for types with the same *used* mapping shape. It does not discover types by convention, scan the project for compatible pairs, or use runtime reflection.

## Contents

- [When to use it](#when-to-use-it)
- [Public API](#public-api)
- [Basic usage](#basic-usage)
- [Generation modes and naming](#generation-modes-and-naming)
- [Compatibility rules](#compatibility-rules)
- [Generated output](#generated-output)
- [Diagnostics](#diagnostics)
- [Implementation architecture](#implementation-architecture)
- [Generation pipeline](#generation-pipeline)
- [Validation details](#validation-details)
- [Known limits](#known-limits)
- [Testing](#testing)

## When to use it

Use `[Adapt]` when a mapping body should be shared by explicitly named type pairs whose members have compatible names and types.

For example, `Person` and `Employee` can both contain the fields used by a template, while `PersonDto` and `EmployeeDto` can both receive the initializer assignments. A single `Person -> PersonDto` template can therefore produce a separate `Employee -> EmployeeDto` API.

Use `[Projectable]` when the generated expression is for the original method's declared signature. Use `[Adapt]` when the generated API should use a different, explicit source and/or destination type. The two attributes can be applied to the same template method.

## Public API

The generator emits the attribute definitions into the consuming compilation during post-initialization. The source definitions live in [`source/Attributes.cs`](../source/Attributes.cs).

```csharp
[Flags]
public enum AdaptGeneration
{
    Map = 1,
    Expression = 2,
    Update = 4
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AdaptAttribute : Attribute
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

This additionally produces `MapEmployee(EmployeeUpdateDto source, Employee dest)`. Any additional template parameters appear before `dest`. Update generation uses the template's `CollectionProperties` policy and has the same value-type and circular-reference safeguards as `[Updatable]`.

An expression factory has no method parameters, so an expression-generating adaptation must supply `Name`. Otherwise the generator reports `AM0011` and skips that adaptation. A map-only adaptation may omit `Name`; in that case it uses the template method name and is emitted as an overload if the adapted first-parameter type makes the signature distinct.

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

public static Expression<Func<Employee, int, EmployeeDto>> MapEmployeeExpression() =>
    (source, currentYear) => new EmployeeDto
    {
        Id = source.Id,
        DisplayName = source.FirstName + " " + source.LastName,
        YearsActive = currentYear - source.StartYear
    };
```

The template's first-parameter *name* is retained, so the body does not need a source-variable rewrite. Its *type* changes because the generated method declares that parameter with the adapted source type. The destination construction is rewritten to the adapted destination type.

Helper methods that can be inlined are expanded before emission. This makes the expression companion useful in LINQ providers that require a self-contained expression tree.

Generated members are written into the same namespace and partial mapper declaration. For nested or generic mapper types, the emitter recreates the containing partial-type hierarchy and generic type parameter lists.

## Diagnostics

All adaptation diagnostics use the `AlephMapper` category. Unless otherwise noted, they are errors and prevent only the invalid adaptation from being generated.

| ID | Severity | Meaning |
| --- | --- | --- |
| `AM0005` | Error | The adapted pair is the same as the template's original source and destination pair. |
| `AM0006` | Error | A member path used from the source cannot be resolved as a readable instance property or field on the adapted source. |
| `AM0007` | Error | A destination initializer member is absent or not writable on the adapted destination. |
| `AM0008` | Error | A directly assigned adapted source member is not implicitly convertible to the adapted destination member. |
| `AM0009` | Error | A generated map or expression name/signature conflicts with an existing or generated member. |
| `AM0010` | Warning | The template does not contain a recognized construction of its declared destination type. |
| `AM0011` | Error | Expression generation was requested without `Name`. |
| `AM0012` | Error | The template declares the same adapted source/destination pair more than once. |
| `AM0013` | Warning | A circular helper reference was found while inlining the template. |
| `AM0014` | Error | The source or destination is an open generic type, or contains a type parameter. |
| `AM0015` | Error | The adapted destination has no compatible constructor for a destination construction in the template. |

The `AM0015` descriptor is named “Generated adapted method fails Roslyn rebinding”; in the current implementation it is specifically reported when the constructor compatibility check fails.

## Implementation architecture

The feature is implemented as part of the incremental source generator. The following components own the adaptation-specific work:

| Component | Responsibility |
| --- | --- |
| [`source/Attributes.cs`](../source/Attributes.cs) | Defines `AdaptAttribute`, `AdaptGeneration`, and the null-rewrite policy. |
| [`source/Generation/AttributeSourceEmitter.cs`](../source/Generation/AttributeSourceEmitter.cs) | Emits the embedded attribute source into each consuming compilation. |
| [`source/Generation/MappingModelFactory.cs`](../source/Generation/MappingModelFactory.cs) | Reads `[Adapt]` attribute data from template methods and creates `AdaptationModel` values. |
| [`source/Models/AdaptationModel.cs`](../source/Models/AdaptationModel.cs) | Holds the Roslyn source and destination symbols, requested name/mode/null policy, and original attribute data. |
| [`source/Generation/Emitters/AdaptationMemberEmitter.cs`](../source/Generation/Emitters/AdaptationMemberEmitter.cs) | Coordinates validation, inlining, rewriting, conflict detection, and member emission. |
| [`source/Adaptation/AdaptationValidator.cs`](../source/Adaptation/AdaptationValidator.cs) | Validates source paths, destination assignments, direct conversions, and constructor compatibility. |
| [`source/Adaptation/AdaptedDestinationRewriter.cs`](../source/Adaptation/AdaptedDestinationRewriter.cs) | Replaces semantic destination constructions with the adapted destination type. |
| [`source/Adaptation/AdaptationMemberPlanner.cs`](../source/Adaptation/AdaptationMemberPlanner.cs) | Reserves generated signatures and detects member conflicts. |
| [`source/Generation/MapperFileEmitter.cs`](../source/Generation/MapperFileEmitter.cs) | Recreates the namespace and containing partial-type hierarchy and renders the generated file. |
| [`source/Diagnostics/DiagnosticDescriptors.cs`](../source/Diagnostics/DiagnosticDescriptors.cs) | Defines `AM0005`–`AM0015`. |

`[Projectable]` and `[Updatable]` are emitted by their own focused emitters. All three features share the mapping model, helper inliner, generated-file context, and output renderer.

## Generation pipeline

The incremental pipeline is registered in [`source/AlephSourceGenerator.cs`](../source/AlephSourceGenerator.cs):

1. `AttributeSourceEmitter` adds `AlephMapper.Attributes.g.cs` after initialization so the consumer can use the attributes.
2. `MappingMethodCandidate` identifies method declarations contained in classes.
3. `MappingModelFactory` filters to static classes, resolves symbols, requires at least one parameter and an expression body, and collects the method's adaptation attributes.
4. `MapperSourceOutput` groups mapping models by containing mapper type. A mapper produces output when it is partial and contains a projectable, updatable, or adapted mapping.
5. For each eligible mapping, the output dispatcher runs `ProjectableMemberEmitter`, `AdaptationMemberEmitter`, and `UpdatableMemberEmitter`.
6. The adaptation emitter processes every `AdaptationModel` independently:
   1. Decodes the requested flags and verifies the naming requirement.
   2. Rejects duplicate source/destination pairs on the same template.
   3. Builds an `InliningResolver` with the adaptation's null policy and inlines helper calls.
   4. Stops with `AM0013` if the inliner detects a circular helper dependency.
   5. Runs `AdaptationValidator` against the original semantic tree.
   6. Rewrites destination constructions in the inlined body.
   7. Reserves the generated signatures through `AdaptationMemberPlanner` and the per-file generated-signature set.
   8. Emits the requested map and/or expression member.
7. `MapperFileEmitter` adds required `using` directives, preserves discovered source `using` directives, recreates enclosing partial classes, and calls `AddSource` with a `*_GeneratedMappings.g.cs` hint name.

## Validation details

### Source paths

The validator walks member-access syntax in the original template and keeps paths rooted at the first parameter. It considers accesses whose resolved symbol is a property or field. For example, `source.Address.City` yields the path `Address.City`.

Each path is resolved segment by segment on the adapted source. A valid segment is a non-static property with a getter or a non-static, non-const field. The lookup walks base types, so inherited members are supported.

Instance method calls are not treated as source-member paths themselves. For `source.Name.Trim()`, `Name` is checked as a readable path; `Trim()` is left to the emitted C# code and normal compilation.

### Destination initializers and conversions

The validator finds explicit (`new Destination(...)`) and target-typed (`new(...)`) creations whose semantic type is the template return type. For every object-initializer assignment, it finds a writable non-static property or field of the same name on the adapted destination.

The explicit conversion check is intentionally narrow: it runs only when the assignment right-hand side is directly a path from the source parameter. In that case, the final adapted source member type must be implicitly convertible to the destination member type according to Roslyn's C# conversion classification. More complex expressions are emitted after structural validation and are then subject to normal C# compilation.

### Constructors

For every explicit destination creation, constructor arguments are compared against constructors on the adapted destination. A constructor is accepted when:

- it does not require more non-optional, non-`params` parameters than supplied;
- it does not receive more arguments than its fixed parameter count unless it has a final `params` parameter; and
- every supplied argument is implicitly convertible to the matching parameter type (or the element type of the `params` array).

Target-typed `new(...)` is also rewritten to an explicit construction of the adapted destination. Constructor compatibility is currently inspected for explicit object-creation syntax; the resulting generated code remains the final C# compiler authority.

### Destination rewriting

The rewriter first finds destination creations from the original body using semantic type equality, rather than trusting the textual spelling of a type. It then replaces those constructions in the inlined body with the adapted destination type.

The special handling for target-typed `new(...)` matters because helper inlining can expand it to an explicit object creation. The rewriter handles both the original implicit form and the expanded explicit form so that `new()` templates continue to generate the adapted destination.

### Collision detection

`AdaptationMemberPlanner` builds a set of ordinary method signatures and non-method names already declared by the mapper. It then reserves each requested generated method signature before any member is emitted. Signatures omit nullable reference-type markers, matching C# overload identity.

The map signature includes the adapted source plus any retained additional parameters. The expression signature is always `<Name>Expression()`. The generator also maintains a file-wide generated-signature set to prevent clashes with members produced by other emitters.

## Known limits

- Adaptation is method-only. Class-level `[Adapt]` is not supported.
- A template must have an expression body and at least one parameter; the first parameter is always the source being adapted.
- Open generic adapted types and adapted types containing type parameters are rejected.
- Validation is structural and purposefully targeted. It does not prove every operator, method call, cast, branch, or arbitrary expression is valid after substitution; the consuming compilation reports any remaining C# errors.
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
