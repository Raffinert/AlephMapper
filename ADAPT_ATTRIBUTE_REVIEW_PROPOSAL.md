# `[Adapt]` Branch Review Proposal

Branch: `adapt-attribute`  
Compared against: `main` (`b574a56`)  
Commits reviewed:

- `43f7a60 Add adapt attribute`
- `5ac0cf8 Enhance source generator for adaptation and add attributes`

Validation run: `dotnet test --no-restore` passes: 60/60 tests.

## Summary

The branch adds a useful explicit structural adaptation feature:

- `AdaptAttribute`
- `AdaptGeneration`
- adapted map generation
- adapted expression generation
- basic source/destination member validation
- duplicate/name conflict diagnostics
- sample app coverage
- two baseline tests: `AdaptBoth`, `AdaptNested`

The direction is good, but the implementation is currently concentrated inside `AlephSourceGenerator.cs` and several declared diagnostics/requirements are not implemented or not tested. Before merging, the feature should be tightened around correctness, validation, diagnostics, and maintainability.

## What is missing

### 1. Generated output is not compilation-validated in tests

`SourceGeneratorTests.GenerationMatchesBaseLine` compares generated files but does not compile the updated compilation or assert generated diagnostics.

This means invalid generated C# can pass as long as the snapshot matches.

Proposal:

- after `RunGeneratorsAndUpdateCompilation`, inspect `outputCompilation.GetDiagnostics()`;
- fail tests for unexpected errors;
- add a separate diagnostics test helper for expected `AMxxxx` diagnostics.

### 2. Several diagnostics are declared but unused

Declared but currently not emitted:

- `AdaptIncompatibleType` (`AM0008`)
- `AdaptUnsupportedSyntax` (`AM0010`)
- `AdaptRebindingFailed` (`AM0015`)

Either implement them or remove/defer them to avoid misleading API surface.

Recommended implementation order:

1. `AM0015`: compile/rebind generated adapted member in-memory and report generator diagnostic instead of letting user see raw generated-code compiler errors.
2. `AM0008`: validate assignment/value compatibility for destination initializer assignments.
3. `AM0010`: detect syntax patterns known not to adapt safely.

### 3. Type compatibility validation is incomplete

Current validation checks:

- source member paths exist;
- top-level destination initializer members are writable;
- source/destination pair is not identical to original;
- adapted types are not open generic.

Missing validation:

- assignment expression type is compatible with adapted destination member type;
- constructor availability for rewritten destination creation;
- constructor argument compatibility;
- nested object initializer compatibility;
- method/operator compatibility after source type substitution;
- conversions/nullability compatibility.

Until this exists, invalid adaptations may produce broken generated source.

### 4. Adaptation support is syntax-limited but not documented as such

The feature works best for simple expression-bodied object initializer templates. It is unclear what happens for:

- constructor-only DTO mappings;
- records / `with` expressions;
- anonymous/intermediate objects;
- collection projections;
- helper methods that depend on the original source/destination concrete type;
- nested destination construction beyond the top-level destination type.

Proposal:

- document a supported syntax matrix;
- emit `AM0010` for unsupported known patterns;
- add tests for each accepted/rejected case.

### 5. Missing diagnostics tests

Add negative test cases for:

- missing adapted source member;
- missing adapted destination member;
- read-only adapted destination member;
- expression generation without `Name`;
- duplicate adapted type pair;
- generated name conflict with existing method;
- generated expression name conflict;
- same source/destination as original mapping;
- open generic adapted type;
- incompatible assignment type;
- constructor mismatch;
- circular helper reference.

### 6. Missing generation-mode tests

Current tests cover `Generate = Both`. Add baseline tests for:

- `Generate = AdaptGeneration.Map` with no `Name`, proving overload generation works;
- `Generate = AdaptGeneration.Map` with custom `Name`;
- `Generate = AdaptGeneration.Expression` with `Name`;
- multiple `[Adapt]` attributes on one method;
- interaction with `[Expressive]` on the same method;
- multi-parameter mapping templates;
- nullable-enabled source files.

### 7. Public API polish

`AdaptAttribute.Name` is non-nullable conceptually optional for map-only generation, but the project currently has nullable annotations disabled and emits CS8632 warnings in other files.

Proposal:

- decide whether the source project should enable nullable globally;
- if not, avoid nullable annotations in generator code;
- document `Name` as optional only when `Generate = Map`;
- consider adding `AdaptGeneration.None = 0` defensively, or explicitly validate unsupported enum values.

### 8. Analyzer warnings increased

`dotnet test` passes but emits analyzer warnings for the newly added diagnostics:

- `RS1032` diagnostic message formatting;
- `RS2008` analyzer release tracking.

Proposal:

- fix message punctuation/formatting;
- either add analyzer release tracking files or suppress/configure `RS2008` consistently for this project.

### 9. Documentation needs consolidation

Committed docs include a very large internal plan file:

- `docs/AlephMapper-Adapt-Attribute-Plan.md` (~1187 lines)

There are also untracked docs:

- `docs/AdaptAttribute.md`
- `docs/adapt_story.md`

Proposal:

- keep one concise user-facing doc, e.g. `docs/adapt.md`;
- move implementation notes to `docs/design/adapt-attribute.md` if still valuable;
- avoid committing duplicate story/plan documents unless linked from README/docs index;
- include supported syntax, diagnostics, examples, and limitations.

## Structure proposal

`AlephSourceGenerator.cs` is now doing extraction, validation, adaptation planning, rewriting, and emission. Split the new Adapt work into focused components.

Suggested layout:

```text
source/
  AlephSourceGenerator.cs                 // incremental pipeline + high-level orchestration only
  Models/
    MappingModel.cs
    AdaptationModel.cs
    AdaptedMappingPlan.cs                 // computed names/types/generation mode
  Adaptation/
    AdaptationCollector.cs                // reads AdaptAttribute data
    AdaptationValidator.cs                // structural checks + diagnostics
    AdaptedBodyRewriter.cs                // source/destination syntax rewriting
    AdaptedMemberEmitter.cs               // emits map/expression members
    AdaptedMemberCompilationValidator.cs  // optional rebind/compile validation
  Generation/
    MapperClassEmitter.cs                 // class/usings/file wrapper
    ExpressionMemberEmitter.cs
    UpdateMemberEmitter.cs
  Diagnostics/
    DiagnosticDescriptors.cs
```

Benefits:

- keeps the generator orchestration readable;
- makes Adapt validation testable independently;
- makes future syntax support incremental;
- reduces risk when changing existing `[Expressive]` / `[Updatable]` behavior.

## Recommended merge plan

### Phase 1: correctness before feature expansion

- compile-validate generated output in tests;
- add negative diagnostics tests;
- implement or remove currently unused diagnostics;
- add generation-mode tests;
- fix analyzer warnings introduced by diagnostics.

### Phase 2: refactor for maintainability

- extract Adapt collector/validator/rewriter/emitter from `AlephSourceGenerator.cs`;
- add small model types for generated member plans;
- keep existing output stable through baseline tests.

### Phase 3: documentation cleanup

- consolidate Adapt docs into one user-facing file;
- clearly document limitations and supported syntax;
- update README or docs index;
- keep sample app example, but avoid overloading it with too many scenarios.

### Phase 4: optional robustness improvements

- add Roslyn rebinding validation for generated adapted members;
- support constructor-only mappings and records deliberately;
- add nullable-aware compatibility checks;
- benchmark impact on generation time for many adaptations.

## Proposed immediate acceptance criteria

Before merging this branch:

- all tests pass;
- generated output is compiled in tests;
- at least one test exists for each new diagnostic that can currently be emitted;
- no declared-but-unused Adapt diagnostics remain unless explicitly marked future/internal;
- map-only, expression-only, and multiple-adaptation scenarios are covered;
- `AlephSourceGenerator.cs` has Adapt-specific logic extracted or a follow-up issue is created and linked;
- Adapt documentation is consolidated and linked.
