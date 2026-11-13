# OSS quality assessment: features

Assessment baseline: commit `a40c4b5`, package version `0.8.2`, reviewed 2026-11-13.

Companion implementation guide: [features-agent-instructions.md](features-agent-instructions.md).

Related assessments: [internal API and design](internal-api.md) and [documentation](docs.md).

## Product direction

Preserve AlephMapper's core model: handwritten, expression-bodied C# mappings generate explicit projection, update, and adaptation companions. Improve predictability, diagnostics, and tested boundaries before increasing the number of supported shapes.

The existing feature set is substantial: nested helper expansion, predicates, extra expression-factory parameters, update overloads, explicit adaptations, supported switch lowering, and C# 14 extension-member companions. Convention discovery or automatic reverse mapping is not a prerequisite for quality.

Priorities: **P1** means a stable-contract improvement; **P2** means a scoped enhancement; **P3** means a demand-driven future capability. The P0 generator defects are tracked in the internal-design assessment.

## F-01 — P1: make transformation semantics enforceable

**Existing contract:** `NullConditionalRewrite.Ignore` removes `?.` from generated expressions by default. `Rewrite` preserves supported null checks but restricts unsafe receivers. Switch lowering and helper substitution may change evaluation count or order. These behaviors are documented and some are intentionally tested.

**Opportunity:** provide an opt-in strict semantic policy that diagnoses transformations unable to meet a defined evaluation contract. Diagnose obvious risky expressions conservatively; do not claim to prove arbitrary C# purity. Even a property getter may have side effects.

Separate three concerns:

1. Valid generated C# and expression-tree construction.
2. Semantic equivalence to ordinary execution for supported inputs.
3. Translation by a particular query provider.

Strict mode should not imply universal SQL translation. Introducing expression blocks or temporary variables to preserve single evaluation may harm provider support and therefore needs an explicit design decision.

Before implementation, specify policy scope, default, attribute placement, class/member precedence, Adapt interaction, diagnostic severity, and whether unsupported companions are omitted. Keep current defaults stable in a compatible release. A safer default null policy belongs in a separately documented breaking-release decision.

Evidence: `docs/advanced-usage.md`, `source/Diagnostics/DiagnosticDescriptors.cs`, and `tests/AlephMapper.Tests/SemanticSafetyGeneratorTests.cs`.

## F-02 — P1: define collection update semantics

**Existing contract:** collections are skipped by default; `CollectionPropertiesPolicy.Update` opts into the generated update path. Documentation warns about replacement on tracked entities. The review found no test explicitly using that enum value.

First document and test what Update actually does for supported shapes: property replacement, resulting reference identity, null input, ordering, and preservation of unrelated destination state. Include EF tracking behavior for a concrete supported entity relationship.

Do not change existing Update behavior into an implicit merge. If users need mutation or keyed reconciliation, design separately named policies. A reconciliation contract must define duplicate keys, missing keys, removals, ordering, null elements, child mapping, and identity retention.

Acceptance criteria: a user can predict the generated update's effect without inspecting generated code, and tests prove the documented effects.

Evidence: `source/Helpers/EmitHelpers.cs`, `source/CodeGenerators/UpdatableMethodGenerator.cs`, `tests/AlephMapper.IntegrationTests/UpdatableTests.cs`, and `docs/advanced-usage.md`.

## F-03 — P1: establish provider translation guarantees

**Existing coverage:** real EF Core SQLite integration tests on .NET 10. The review found no SQL-capture or SQL-shape assertions in the integration tests. Query execution is useful evidence but does not rule out computation in the top-level client projection.

Add representative translation checks for computed scalar projections, predicates, null navigation, and nested projections. Assert the relevant SQL structure or executed command and result; avoid brittle full SQL snapshots when aliases and formatting are immaterial.

For a computed scalar, assert that the database performs the operation and that the expected columns are selected. For predicates, prove filtering occurs server-side. SQL text cannot directly show an arbitrary client method call, so checking for the absence of a C# method name is not a valid fallback detector.

Then add one additional provider based on real usage, with pinned test dependencies, reproducible setup, and explicit skip/failure reporting. State support as tested feature/provider/version combinations, not “all EF providers supported.”

Evidence: `tests/AlephMapper.IntegrationTests/AlephMapper.IntegrationTests.csproj`, `MultiParamTests.cs`, and `SimpleTests.cs`.

## F-04 — P2: make diagnostics a first-class feature

The existing diagnostic catalog already distinguishes missing adapted members, incompatible assignments, unsupported shapes, unsafe null receivers, mapper declaration errors, and member conflicts.

Improve actionable locations, explanations, stable documentation links, and recovery examples. When adaptation rebinding fails, surface the most useful original-source context rather than only a generated-code compiler message. Preserve precise existing diagnostics instead of adding redundant generic warnings.

Add narrowly scoped IDE fixes for common mistakes only after defining the analyzer/code-fix packaging and compatibility requirements. A fix must preserve user code and compile after application. Adding an attribute or `partial` can be safe in a validated context; automatically rewriting arbitrary method bodies is a different scope.

Evidence: `source/Diagnostics/DiagnosticDescriptors.cs`, `docs/Adapt-Attribute.md`, and existing declaration/signature tests.

## F-05 — P3: expand generic and language support from demand

Potential candidates include generic C# 14 extension blocks/methods and generic adaptation. These are currently explicit boundaries, not accidental omissions.

Prioritize a concrete use case before implementation. Specify generated signatures, type constraints, inference, overload resolution, collision handling, nullability, and behavior across compiler bands. Unsupported input must remain diagnosable; do not loosen validation before generation is correct.

Do not assume that broader syntax acceptance automatically improves provider translation.

## Recommended delivery sequence

1. Complete the internal-design P0 fixes.
2. Implement collection contract tests and provider SQL assertions.
3. Design strict semantics and improve diagnostic usability.
4. Add IDE fixes once packaging and compatibility are defined.
5. Select additional provider or generic support from demonstrated demand.

Existing CI already verifies packed analyzer selection under Roslyn 4.8, 4.14, 5.0, and the SDK compiler. Extend those checks for new generated APIs; do not create a redundant compatibility framework.

## Completion standard

A shipped feature has a written contract, positive and negative tests, compilable generated output, runtime or provider verification where applicable, documented limitations, and a compatibility decision. Proposed features are not complete merely because a design document exists, and a design document must not present unimplemented APIs as available.
