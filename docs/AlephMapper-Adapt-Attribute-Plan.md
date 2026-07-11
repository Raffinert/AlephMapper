# AlephMapper `[Adapt]` Attribute — Implementation Plan

## 1. Goal

Add an `[Adapt]` attribute to AlephMapper that reuses an existing mapping method as a compile-time template for one explicitly specified source type and one explicitly specified destination type.

`[Adapt]` must not scan the compilation or assembly for candidate types.

The adapted source and destination types are always declared directly in the attribute.

The generator must validate that the explicitly specified types are structurally compatible with the mapping operations used by the original method.

---

## 2. Core Semantics

AlephMapper already treats a manually written mapping method as the single source of truth.

For example:

```csharp
public static PersonDto MapPerson(Person source) => new()
{
    Id = source.Id,
    Name = source.FirstName + " " + source.LastName,
    Email = source.Email
};
```

`[Adapt]` allows that mapping implementation to be reused for another explicit type pair:

```csharp
public static partial class PersonMapper
{
    [Adapt(
        typeof(Employee),
        typeof(EmployeeDto),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Both)]
    public static PersonDto MapPerson(Person source) => new()
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName,
        Email = source.Email
    };
}
```

Generated output:

```csharp
public static partial class PersonMapper
{
    public static EmployeeDto MapEmployee(Employee source) =>
        new EmployeeDto
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
}
```

The original method remains unchanged.

---

## 3. Difference Between `[Expressive]` and `[Adapt]`

| Attribute | Purpose |
|---|---|
| `[Expressive]` | Generates an expression for the exact parameter and return types declared by the method |
| `[Adapt]` | Generates a regular mapping method and/or expression for one explicitly declared source/destination pair |

Example:

```csharp
[Expressive]
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Name = "MapEmployee",
    Generate = AdaptGeneration.Both)]
public static PersonDto MapPerson(Person source) => ...;
```

Generated APIs:

```csharp
// Original method
PersonDto MapPerson(Person source)

// From [Expressive]
Expression<Func<Person, PersonDto>> MapPersonExpression()

// From [Adapt]
EmployeeDto MapEmployee(Employee source)
Expression<Func<Employee, EmployeeDto>> MapEmployeeExpression()
```

`[Expressive]` must continue to operate only on the original declared method types.

`[Adapt]` must not change the behavior or meaning of `[Expressive]`.

---

## 4. Non-Goals

The first implementation must not:

- scan the assembly for compatible types;
- scan the compilation for candidate source/destination pairs;
- pair types based on naming conventions;
- automatically discover DTO/entity pairs;
- generate mappings for unspecified types;
- use runtime reflection;
- perform runtime shape validation.

Every adapted pair must be explicitly declared with `typeof(...)`.

---

## 5. Proposed Public API

### 5.1 Generation mode

```csharp
[Flags]
public enum AdaptGeneration
{
    Map = 1,
    Expression = 2,
    Both = Map | Expression
}
```

### 5.2 Attribute

```csharp
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class AdaptAttribute : Attribute
{
    public AdaptAttribute(
        Type sourceType,
        Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }

    public Type SourceType { get; }

    public Type DestinationType { get; }

    public string? Name { get; set; }

    public AdaptGeneration Generate { get; set; } =
        AdaptGeneration.Both;

    public NullConditionalRewrite NullConditionalRewrite { get; set; } =
        NullConditionalRewrite.Ignore;
}
```

### 5.3 Why method-only

`[Adapt]` should initially be allowed only on methods.

A class-level `[Adapt]` would be ambiguous because it would not identify which mapping method should be used as the template.

### 5.4 Why `AllowMultiple = true`

A single mapping method may be adapted to several explicit pairs:

```csharp
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Name = "MapEmployee")]

[Adapt(
    typeof(Customer),
    typeof(CustomerDto),
    Name = "MapCustomer")]

public static PersonDto MapPerson(Person source) => new()
{
    Id = source.Id,
    Name = source.Name
};
```

No pair is discovered automatically.

---

## 6. Naming Rules

The `Name` property defines the base name of the generated members.

Example:

```csharp
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Name = "MapEmployee")]
```

Generates:

```csharp
EmployeeDto MapEmployee(Employee source)

Expression<Func<Employee, EmployeeDto>>
    MapEmployeeExpression()
```

Recommended rules:

1. `Name` is required when `Generate` includes `Expression`.
2. `Name` may be omitted for map-only adaptations.
3. For map-only adaptations without `Name`, use the original method name as an overload.
4. Report a diagnostic when a generated method name conflicts with an existing member.
5. Do not silently append suffixes such as `1`, `2`, or `Generated`.
6. Multiple adaptations must not generate identical signatures.
7. Expression methods always need distinct names because they have no mapping parameters.

Example of valid map-only overloading:

```csharp
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Generate = AdaptGeneration.Map)]
public static PersonDto Map(Person source) => ...;
```

Generated:

```csharp
public static EmployeeDto Map(Employee source) => ...;
```

---

## 7. Definition of Shape Compatibility

Shape compatibility must be based on the members and operations actually used by the original mapping method.

It must not require the full source or destination types to have identical property sets.

### 7.1 Example template

```csharp
public static PersonDto Map(Person source) => new()
{
    Id = source.Id,
    DisplayName = source.FirstName + " " + source.LastName
};
```

The adapted source must provide compatible members for:

- `Id`
- `FirstName`
- `LastName`

The adapted destination must provide compatible writable members for:

- `Id`
- `DisplayName`

Additional members on either type are irrelevant.

### 7.2 Source compatibility

For every member access rooted in the original source parameter:

```csharp
source.Id
source.Address.City
source.Department.Name
```

The adapted source must expose the equivalent member path:

```text
Id
Address.City
Department.Name
```

Validation must include:

- member name;
- accessibility;
- property or field readability;
- static versus instance member semantics;
- indexer compatibility where supported;
- operation compatibility at the leaf expression;
- nullability compatibility where relevant.

### 7.3 Destination compatibility

For every object initializer assignment:

```csharp
new PersonDto
{
    Id = ...,
    Name = ...
}
```

The adapted destination must have accessible writable members named:

- `Id`
- `Name`

The rewritten right-hand expression must be implicitly convertible to the adapted destination member type.

### 7.4 Extra properties

Extra source or destination properties do not affect compatibility.

Compatible:

```csharp
public sealed class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
}

public sealed class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

### 7.5 Incompatible source example

```csharp
public sealed class Employee
{
    public int Id { get; set; }

    // The template performs string operations on Name.
    public EmployeeName Name { get; set; }
}
```

### 7.6 Incompatible destination example

```csharp
public sealed class EmployeeDto
{
    public int Id { get; set; }

    // The template produces a string.
    public int Name { get; set; }
}
```

---

## 8. Template Method Requirements

Initially, adapted methods should follow AlephMapper's existing mapping-method requirements:

- declared in a `static partial` class;
- method is `static`;
- method is expression-bodied;
- method has at least one parameter;
- first parameter is treated as the source parameter;
- method has a non-void return type;
- method is visible to the generator in the current compilation.

The first version should reject:

- generic mapping methods;
- open generic adapted source or destination types;
- anonymous destination types;
- `ref`, `in`, or `out` source parameters;
- pointer types;
- function-pointer types;
- dynamic source or destination types;
- methods without expression bodies.

---

## 9. Additional Method Parameters

Only the first method parameter is adapted.

Example template:

```csharp
public static PersonDto Map(
    Person source,
    string culture,
    bool includeEmail) => ...;
```

Adapted output:

```csharp
public static EmployeeDto MapEmployee(
    Employee source,
    string culture,
    bool includeEmail) => ...;

public static Expression<
    Func<Employee, string, bool, EmployeeDto>>
    MapEmployeeExpression() => ...;
```

The types of additional parameters remain unchanged.

Their names and order remain unchanged.

---

## 10. Generator Data Model

Add an adaptation model:

```csharp
internal sealed record AdaptationModel(
    INamedTypeSymbol SourceType,
    INamedTypeSymbol DestinationType,
    string? GeneratedName,
    AdaptGeneration Generation,
    NullConditionalRewrite NullStrategy,
    AttributeData Attribute);
```

Extend `MappingModel`:

```csharp
public IReadOnlyList<AdaptationModel> Adaptations { get; }
```

Possible supporting models:

```csharp
internal sealed record MemberAdaptation(
    ISymbol TemplateMember,
    ISymbol AdaptedMember);

internal sealed record AdaptationContext(
    MappingModel Mapping,
    AdaptationModel Adaptation,
    IReadOnlyDictionary<ISymbol, ISymbol> SourceMemberBindings,
    IReadOnlyDictionary<ISymbol, ISymbol> DestinationMemberBindings);

internal sealed record ShapeCompatibilityResult(
    bool IsCompatible,
    IReadOnlyList<Diagnostic> Diagnostics);
```

---

## 11. Attribute Parsing

For every mapping method:

1. Retrieve all `[Adapt]` attributes.
2. Read constructor argument 0 as source type.
3. Read constructor argument 1 as destination type.
4. Read named argument `Name`.
5. Read named argument `Generate`.
6. Read named argument `NullConditionalRewrite`.
7. Validate that both type arguments are valid named types.
8. Validate that neither type is open generic.
9. Create one `AdaptationModel` per attribute.

Do not enumerate symbols in the compilation looking for candidate types.

---

## 12. Adaptation Pipeline

### Step 1: Build the normal mapping model

Reuse the existing method discovery and semantic analysis used for `[Expressive]` and `[Updatable]`.

### Step 2: Parse explicit adaptations

Attach all `[Adapt]` instances to the mapping model.

### Step 3: Validate the template method

Check the method shape and unsupported constructs.

### Step 4: Inline helper mappings

Run the existing `InliningResolver` before adaptation.

Example:

```csharp
public static PersonDto Map(Person source) => new()
{
    Name = FormatName(source)
};

private static string FormatName(Person source) =>
    source.FirstName + " " + source.LastName;
```

After inlining:

```csharp
new PersonDto
{
    Name = source.FirstName + " " + source.LastName
}
```

The shape analyzer can then validate `FirstName` and `LastName` directly against the explicit adapted source type.

### Step 5: Collect source member paths

Find every member access rooted in the first source parameter.

Examples:

```csharp
source.Id
source.Address.City
source.Department.Name
```

Resolve each original member through Roslyn semantic symbols.

### Step 6: Bind source member paths

For each original source member path:

1. Start from the explicit adapted source type.
2. Find an accessible member with the same name.
3. Continue recursively through the member path.
4. Validate readability.
5. Record the original/adapted symbol mapping.
6. Report a diagnostic when any segment cannot be resolved.

### Step 7: Collect destination assignments

Inspect the object initializer or supported construction syntax.

For every assigned destination member:

1. Resolve the original destination member.
2. Find the same-named writable member on the explicit adapted destination.
3. Validate accessibility and writability.
4. Record the binding.
5. Report a diagnostic on failure.

### Step 8: Rewrite the mapping body

Introduce:

```csharp
internal sealed class AdaptationRewriter : CSharpSyntaxRewriter
```

The rewriter should replace:

- original destination object creation with the explicit adapted destination type;
- source member symbols with their adapted equivalents;
- destination member symbols with their adapted equivalents;
- casts involving original source/destination types where substitution is valid;
- nested target-typed `new()` when contextual destination types are known.

### Step 9: Rebind generated syntax

Create a temporary syntax tree or generated method declaration and ask Roslyn to bind it.

This final compilation check validates:

- member resolution;
- overload resolution;
- binary and unary operators;
- implicit conversions;
- method invocation compatibility;
- constructor availability;
- destination assignment compatibility;
- expression-tree compatibility.

### Step 10: Emit diagnostics

Prefer AlephMapper-specific diagnostics with clear messages.

Compiler rebinding may be used as a final correctness gate, but users should not receive only opaque compiler errors when the generator can provide a better explanation.

### Step 11: Emit requested methods

Generate:

- regular map method;
- expression method;
- or both.

---

## 13. Rewriter Design

Suggested API:

```csharp
internal sealed class AdaptationRewriter : CSharpSyntaxRewriter
{
    public AdaptationRewriter(AdaptationContext context)
    {
        _context = context;
    }
}
```

The rewriter should be symbol-driven, not text-driven.

Do not replace all identifiers named `Id`, `Name`, or `Address`.

Only rewrite syntax nodes whose bound symbols match recorded original symbols.

This avoids corrupting:

- local variables;
- unrelated method calls;
- members on additional parameters;
- shadowed identifiers;
- nested lambda parameters;
- static members with matching names.

---

## 14. Nested Source Shapes

Nested source paths should be supported early.

Template:

```csharp
source.Address.City
```

Original types:

```text
Person.Address -> Address
Address.City   -> string
```

Adapted types:

```text
Employee.Address -> EmployeeAddress
EmployeeAddress.City -> string
```

The intermediate types do not need to be identical.

Only the required member path must be structurally compatible.

No type scanning is needed because the path begins at the explicitly specified adapted source type.

---

## 15. Nested Destination Shapes

Template:

```csharp
new PersonDto
{
    Address = new AddressDto
    {
        City = source.Address.City
    }
}
```

Adapted output:

```csharp
new EmployeeDto
{
    Address = new EmployeeAddressDto
    {
        City = source.Address.City
    }
}
```

The nested adapted destination type can be derived from assignment context:

```text
PersonDto.Address type   = AddressDto
EmployeeDto.Address type = EmployeeAddressDto
```

This enables recursive adaptation without scanning for related type pairs.

Recommended implementation order:

1. top-level destination object initializer;
2. nested source member paths;
3. nested destination initializers;
4. collection element adaptation;
5. constructor-based destinations.

---

## 16. Collections

Collection support should be introduced after scalar and nested object adaptation.

Example template:

```csharp
new PersonDto
{
    Children = source.Children
        .Select(MapChild)
        .ToList()
}
```

A nested mapping call should only be adapted when a corresponding explicit `[Adapt]` exists.

Example:

```csharp
[Adapt(
    typeof(EmployeeChild),
    typeof(EmployeeChildDto),
    Name = "MapEmployeeChild")]
public static ChildDto MapChild(Child source) => ...;
```

The generator must not discover child pairs automatically.

Possible future adapted output:

```csharp
Children = source.Children
    .Select(MapEmployeeChild)
    .ToList()
```

or an inlined equivalent when expression generation requires it.

---

## 17. Interaction With Helper Inlining

Existing helper-method inlining should happen before adaptation.

Benefits:

- shape analysis sees the complete member usage;
- generated expressions remain EF Core-friendly;
- adapted mappings reuse existing AlephMapper behavior;
- circular helper references can be diagnosed consistently.

If helper inlining fails because of a circular reference, adaptation for that method should be skipped with a diagnostic.

---

## 18. Null Handling

`[Adapt]` should support the same null-conditional rewrite strategies as `[Expressive]`:

```csharp
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Name = "MapEmployee",
    NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
```

Policies:

| Policy | Behavior |
|---|---|
| `None` | Preserve unsupported null-conditional syntax and report when expression generation cannot support it |
| `Ignore` | Rewrite `source.Address?.City` as `source.Address.City` |
| `Rewrite` | Rewrite to explicit null checks |

The adaptation pipeline should apply the selected null strategy consistently to generated map and expression bodies.

---

## 19. Proposed Diagnostics

AlephMapper currently uses diagnostic IDs starting at `AM0001`.

Suggested new diagnostics:

| ID | Severity | Description |
|---|---:|---|
| `AM0005` | Error | Invalid adapted source or destination type |
| `AM0006` | Error | Required source member is missing |
| `AM0007` | Error | Required destination member is missing or not writable |
| `AM0008` | Error | Adapted expression or assignment has an incompatible type |
| `AM0009` | Error | Generated method name or signature conflicts |
| `AM0010` | Warning | Template contains unsupported adaptation syntax |
| `AM0011` | Error | Expression generation requested without a valid generated name |
| `AM0012` | Error | Duplicate adaptation for the same explicit type pair |
| `AM0013` | Warning | Circular helper reference prevents adaptation |
| `AM0014` | Error | Adapted source or destination type is open generic |
| `AM0015` | Error | Generated adapted method fails Roslyn rebinding |

Example:

```text
AM0006: Cannot adapt 'MapPerson' from 'Person' to 'Employee':
source member path 'Address.City' cannot be resolved because
'EmployeeAddress' does not contain an accessible member named 'City'.
```

Example:

```text
AM0008: Cannot assign adapted expression of type 'string' to
'EmployeeDto.Id' of type 'int'.
```

Diagnostics should point to:

1. the relevant `[Adapt]` attribute;
2. the original mapping expression as an additional location where useful.

---

## 20. Suggested Project Structure

```text
source/
├── Models/
│   ├── AdaptationModel.cs
│   ├── AdaptationContext.cs
│   ├── MemberAdaptation.cs
│   └── ShapeCompatibilityResult.cs
├── Analysis/
│   ├── AdaptationAttributeParser.cs
│   ├── SourceShapeAnalyzer.cs
│   ├── DestinationShapeAnalyzer.cs
│   └── AdaptedBodyValidator.cs
├── SyntaxRewriters/
│   └── AdaptationRewriter.cs
├── CodeGenerators/
│   └── AdaptedMethodGenerator.cs
└── Diagnostics/
    └── DiagnosticDescriptors.cs
```

Responsibilities:

### `AdaptationAttributeParser`

- reads all `[Adapt]` attributes;
- validates constructor arguments;
- builds `AdaptationModel` instances.

### `SourceShapeAnalyzer`

- identifies source-rooted member paths;
- resolves matching members on the explicit adapted source type;
- creates source symbol bindings.

### `DestinationShapeAnalyzer`

- identifies assigned destination members;
- resolves matching writable members on the explicit adapted destination type;
- creates destination symbol bindings.

### `AdaptationRewriter`

- rewrites source and destination symbols;
- substitutes object creation types;
- preserves unrelated symbols.

### `AdaptedBodyValidator`

- binds generated syntax using Roslyn;
- validates conversions, calls, operators, and constructors;
- reports final compatibility diagnostics.

### `AdaptedMethodGenerator`

- emits regular map methods;
- emits expression methods;
- produces XML documentation;
- handles names and signatures.

---

## 21. Incremental Generator Integration

The current generator is method-centric, which is a good fit for `[Adapt]`.

Recommended flow:

```text
Syntax candidate
    ↓
MappingModel
    ↓
Read [Expressive], [Updatable], and [Adapt]
    ↓
Group mapping models by containing mapper class
    ↓
Inline method body
    ↓
Generate exact-type companions
    ↓
For each explicit AdaptationModel:
        analyze shape
        rewrite body
        rebind generated syntax
        emit requested adapted companions
```

No global type index is required.

The only symbols needed for adaptation are:

- original method symbol;
- original parameter and return types;
- explicit adapted source type;
- explicit adapted destination type;
- members referenced from the original method body.

---

## 22. First Release Scope

### Supported

- method-level `[Adapt]`;
- multiple `[Adapt]` attributes per method;
- explicit `typeof(source)` and `typeof(destination)`;
- map-only generation;
- expression-only generation;
- generation of both;
- top-level object initializers;
- target-typed `new()`;
- explicit destination constructors without arguments;
- scalar member access;
- nested source paths;
- additional unchanged method parameters;
- helper-method inlining;
- null-conditional policies;
- compile-time diagnostics;
- nullable enabled and disabled projects.

### Deferred

- nested destination object substitution;
- collection element adaptation;
- constructor argument remapping;
- records with positional constructors;
- generic template methods;
- custom member-name configuration;
- custom converters;
- explicit source-member aliases;
- conditional attribute selection;
- cross-method automatic adaptation discovery.

---

## 23. Delivery Phases

## Phase 1 — Attribute and Basic Top-Level Adaptation

Implement:

- `AdaptAttribute`;
- `AdaptGeneration`;
- repeated method-level attributes;
- attribute parser;
- explicit type validation;
- top-level source member binding;
- top-level destination member binding;
- regular map generation;
- expression generation;
- naming diagnostics;
- baseline tests.

Acceptance example:

```csharp
[Adapt(
    typeof(Employee),
    typeof(EmployeeDto),
    Name = "MapEmployee",
    Generate = AdaptGeneration.Both)]
public static PersonDto MapPerson(Person source) => new()
{
    Id = source.Id,
    Name = source.Name
};
```

## Phase 2 — Nested Structural Adaptation

Implement:

- nested source member paths;
- inherited members;
- nullable compatibility;
- nested destination initializers;
- target-typed nested `new()`.

## Phase 3 — Collections and Nested Mapping Calls

Implement:

- collection source compatibility;
- explicit nested adaptation lookup;
- rewriting mapped collection calls;
- expression-safe collection projections.

No nested pair may be inferred automatically.

## Phase 4 — Constructors and Advanced Syntax

Implement:

- constructor argument remapping;
- positional records;
- switch expressions;
- conditional expressions;
- casts;
- user-defined conversions;
- advanced overload rebinding.

---

## 24. Test Plan

The existing source-generator baseline-test structure can be extended with a new `Files/Adapt` family.

Suggested test cases:

```text
AdaptMapOnly
AdaptExpressionOnly
AdaptBoth
AdaptMultipleExplicitPairs
AdaptAdditionalParameters
AdaptOriginalMethodAlsoExpressive
AdaptTargetTypedNew
AdaptExplicitDestinationCreation
AdaptNestedSourceShape
AdaptNestedDestinationShape
AdaptHelperInlining
AdaptNullConditionalIgnore
AdaptNullConditionalRewrite
AdaptNullableEnabled
AdaptNullableDisabled
AdaptInheritedSourceMember
AdaptInheritedDestinationMember
AdaptMissingSourceMember
AdaptMissingNestedSourceMember
AdaptMissingDestinationMember
AdaptReadOnlyDestinationMember
AdaptIncompatibleAssignment
AdaptIncompatibleOperator
AdaptInvalidConstructor
AdaptDuplicatePair
AdaptGeneratedNameCollision
AdaptExpressionWithoutName
AdaptOpenGenericSource
AdaptOpenGenericDestination
AdaptCircularHelper
AdaptCollection
AdaptNestedExplicitMapping
```

### Integration tests

Regular mapping:

```csharp
var dto = Mapper.MapEmployee(employee);
```

Expression mapping:

```csharp
var result = db.Employees
    .Select(Mapper.MapEmployeeExpression())
    .ToList();
```

Behavior assertions:

- generated map result matches original template semantics;
- generated expression compiles;
- EF Core accepts supported generated expressions;
- unsupported shapes produce AlephMapper diagnostics;
- no unrelated types are inspected or generated.

---

## 25. Acceptance Criteria

The feature is complete when this source:

```csharp
public static partial class Mapper
{
    [Adapt(
        typeof(Employee),
        typeof(EmployeeDto),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Both)]
    public static PersonDto MapPerson(Person source) => new()
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName
    };
}
```

produces:

```csharp
public static EmployeeDto MapEmployee(Employee source) =>
    new EmployeeDto
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName
    };

public static Expression<Func<Employee, EmployeeDto>>
    MapEmployeeExpression() =>
        source => new EmployeeDto
        {
            Id = source.Id,
            Name = source.FirstName + " " + source.LastName
        };
```

The generator must fail with a clear AlephMapper diagnostic when:

- `Employee` does not expose a required source member;
- `EmployeeDto` does not expose a required writable destination member;
- an adapted operation no longer compiles;
- a generated assignment is not type-compatible;
- a generated member name conflicts;
- the explicit source or destination type is invalid.

---

## 26. Final Design Principle

`[Adapt]` is an explicit compile-time structural substitution mechanism.

It takes:

1. one existing mapping method;
2. one explicitly specified source type;
3. one explicitly specified destination type;

and generates a regular mapping method, an expression method, or both.

It never searches for candidate types.
