# Implementation instructions: features

Read [features.md](features.md) first. This guide separates contract hardening from optional new APIs. Coordinate with the [internal-design guide](internal-api-agent-instructions.md) and [documentation guide](docs-agent-instructions.md).

## Working rules

1. Read the active task, current repository instructions, and current implementation before editing.
2. Run `git status --short`; preserve other work. Do not infer authorization to publish, change defaults, or perform a major-version migration from this plan.
3. Preserve the handwritten mapping model and explicit adaptation model.
4. Use existing TUnit, Roslyn, EF Core, and package compatibility infrastructure.
5. Never invent an API name in user-facing documentation before the API is implemented and tested.
6. Do not treat `IQueryable` execution, expression compilation, and SQL translation as equivalent checks.
7. Work on one feature contract at a time. Track work with the F identifiers in the assessment.

## Step 1 — Establish baseline and boundaries

Read:

- `source/Attributes.cs`
- `source/Generation/MappingAnalysisFactory.cs`
- `source/Diagnostics/DiagnosticDescriptors.cs`
- `docs/advanced-usage.md`
- `docs/Adapt-Attribute.md`
- `docs/extension-members-compatibility-matrix.md`
- `tests/AlephMapper.IntegrationTests/AlephMapper.IntegrationTests.csproj`
- `tests/PackageCompatibility/Consumer.csproj`

Run `dotnet test --configuration Release` and record results. Confirm whether IA-01 and IA-02 are fixed before attempting broad feature expansion. Existing unrelated defects need to be recorded, not worked around through weakened assertions.

Create a checklist separating:

- Existing behavior to document and test.
- Compatible improvements with no new public policy.
- New APIs that require a design specification.
- Breaking changes outside the default scope.

**Checkpoint:** every planned edit belongs to one of those categories and an F identifier.

## Step 2 — Specify and test collection updates (F-02)

This is contract hardening, not authorization to introduce keyed merging.

1. Read `source/Helpers/EmitHelpers.cs` and `source/CodeGenerators/UpdatableMethodGenerator.cs`.
2. Follow the branch selected by `CollectionPropertiesPolicy.Skip` and `Update`.
3. Read existing update tests, especially `tests/AlephMapper.IntegrationTests/UpdatableTests.cs`.
4. Add a minimal mapper whose initializer includes a collection and whose attribute opts into Update.
5. Inspect the generated overload and record whether it assigns a replacement collection, mutates an existing collection, or behaves differently for different supported shapes.
6. Build a runtime test matrix before revising documentation.

Required cases:

| Case | Assert |
| --- | --- |
| Default Skip with a populated destination | Original collection reference and contents remain unchanged |
| Update with populated source and destination | Exact resulting contents and reference identity |
| Empty source collection | Documented empty/replacement behavior |
| Nullable source collection and guarded mapping | Documented null behavior without accidental dereference |
| Unmapped destination property | Existing value remains unchanged |
| Supported mapped child objects | Expected child values and identity behavior |

For EF tracking, use an existing relationship fixture or add a minimal one. Load a tracked parent and children, apply the update, inspect tracking states, save, and reload in a fresh context. Assert the actual persisted relationship behavior. Do not generalize one relationship configuration to all EF models.

Update `docs/advanced-usage.md` with the verified behavior and a concise example. Explain what Update means; do not call it a merge unless it actually merges.

**Checkpoint:** tests explicitly mention `CollectionPropertiesPolicy.Update`, prove reference/content behavior, and agree with the documentation.

## Step 3 — Add SQL translation assertions (F-03)

1. Reuse the SQLite integration setup.
2. Choose a scalar computed projection already supported by the mapper, such as combining two string columns.
3. Capture SQL using `ToQueryString()` or the existing EF command interception infrastructure if available.
4. Assert that the relevant operation occurs in SQL and that the selected columns match the intended projection.
5. Execute the query and assert results. SQL shape alone is not enough.
6. Add a generated predicate case and prove filtering is represented in SQL.
7. Add a null-navigation case and, where supported, a nested projection case.
8. Avoid assertions on incidental aliases, whitespace, or ordering unless those are the behavior under test.

Construct a control case demonstrating why the assertion matters: a normal client-computed top-level projection may execute successfully while selecting its input columns. Ensure the positive generated case is distinguished by the relevant SQL computation. Do not inspect SQL merely for the absence of a C# method name; client code is absent from SQL by definition.

If adding another provider is part of the active task:

1. Choose the provider from user requirements or recorded adoption evidence.
2. Pin dependencies and document the database prerequisite.
3. Use isolated test data and deterministic setup/teardown.
4. Keep provider-specific expectations explicit.
5. Make missing infrastructure visible as a skip or setup failure; do not silently report success.
6. Add CI setup only when the environment can actually run it.

**Checkpoint:** the tested translation claims are narrower and stronger than “query execution succeeded.”

## Step 4 — Specify strict semantic validation before coding (F-01)

This step produces a design decision. It does not introduce a breaking default change.

Read the semantic-safety tests, null-rewrite diagnostics, and the helper/switch rewriters. Write a short specification answering each question:

1. Which companion types are covered: projections, updates, adaptations, or a subset?
2. Which transformations can duplicate, omit, or reorder evaluation?
3. What syntax/symbol evidence is enough to accept a transformation conservatively?
4. How are property getters and method calls handled when purity is unknown?
5. Does an unsafe transformation cause a warning, an error, or omission of a companion?
6. How does the option interact with Ignore, Rewrite, and None?
7. Where is the option configured, and what is class/member precedence?
8. How does it interact with Adapt's explicit options?
9. Which behavior remains unchanged when the option is absent?
10. Which provider-compatible expression shapes will be generated?

Use these illustrative cases when defining expected behavior:

- A helper uses the same literal argument twice.
- A helper uses an ordinary parameter twice.
- A helper uses a method-call argument twice.
- A helper ignores an argument whose evaluation has a side effect.
- A switch governing expression calls a counter-incrementing method.
- A receiver is a property getter whose evaluation may be observable.

Do not promise a general purity proof. If a contract cannot be defined without unresolved product choices, record the options and the exact decision needed. Continue independent collection, SQL, or diagnostic work; do not guess a breaking policy.

**Checkpoint:** an implementer can determine the expected result and diagnostic for each case without improvising policy.

## Step 5 — Implement strict validation only within the agreed scope

If the active task includes the designed API:

1. Add the smallest public option consistent with the specification.
2. Verify it is emitted correctly through the existing embedded attribute mechanism and both compiler bands.
3. Allocate diagnostics through the existing catalog and release-tracking convention; preserve existing IDs.
4. Put semantic checks before emitting a companion whose behavior would violate the chosen policy.
5. Prefer a precise source location and recovery text.
6. Add positive, negative, default-compatibility, precedence, and Adapt-interaction tests.
7. Compare ordinary and generated execution for accepted examples.
8. Verify provider translation for any newly generated expression shape.
9. Document the exact limits and defaults.

Do not inject blocks or temporary-variable expression nodes merely to obtain in-memory equivalence if the feature promises provider-compatible output. Make that distinction explicit in the design.

**Checkpoint:** absence of the new option preserves existing behavior, and enabled behavior matches the specification.

## Step 6 — Improve diagnostic usability (F-04)

1. Inventory current descriptors and identify the most confusing real failure cases.
2. Add tests that assert diagnostic ID, severity, actionable source span, and omission/presence of generated companions.
3. Improve messages and source locations without changing IDs casually.
4. For adaptation rebinding errors, retain the underlying useful cause and point back to the relevant original mapping where feasible.
5. Do not emit generic warnings for every complex expression merely because dedicated validation is incomplete.
6. Coordinate stable documentation anchors with D-03.
7. Verify repeated or incremental runs do not leave stale diagnostic locations.

If IDE code fixes are explicitly in scope, first define their assembly/package placement and host compatibility. Keep Workspaces-dependent code out of the compiler-only generator path unless dependencies are deliberately supported. Test the complete apply-fix result, including compilation and trivia preservation. Start with one validated declaration fix; do not rewrite arbitrary bodies.

**Checkpoint:** a user can act on each improved diagnostic without reading generated implementation details.

## Step 7 — Gate broader language features (F-05)

Do this only when a concrete use case is selected.

1. Capture the smallest user source that needs the feature.
2. Specify the exact generated APIs, constraints, inference, overload behavior, and nullability.
3. Create a compiler-band support table.
4. Test invalid and ambiguous forms before relaxing current validators.
5. Implement analysis, generation, and conflict detection as one coherent change.
6. Add package-consumer tests that call the generated members.
7. Update compatibility matrices only after the tests pass.

Generic extension support and generic adaptation are separate projects. Do not implement both merely because this assessment names them.

## Step 8 — Verify and report

Run focused tests during implementation. Run the full Release suite once the final relevant changes are in place. For changes to attributes, generated APIs, or compiler-dependent syntax, run the existing package compiler verification:

```powershell
dotnet test --configuration Release
dotnet pack source/AlephMapper.csproj --configuration Release --output artifacts/oss-quality-features-package
python scripts/verify_roslyn_compatibility.py --package-dir artifacts/oss-quality-features-package
git diff --check
```

Use a fresh output directory if the chosen one contains other package versions. Do not delete unrelated files. Record actual provider versions and whether tests ran or were skipped.

The final report must identify completed F items, design-only items, remaining optional features, compatibility implications, validation results, and documentation updated. “Designed” and “implemented” are different statuses.
