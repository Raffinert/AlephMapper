# Implementation instructions: internal API and design

Read [internal-api.md](internal-api.md) first. This guide turns IA-01 through IA-06 into ordered implementation tasks. It records the baseline plan; check the assessment's implementation update and current checkout before treating a step as outstanding.

## Working rules

1. Read the current user request and applicable repository instructions. They take precedence over this plan.
2. Work from the repository root. Run `git status --short` and record the current commit. Preserve pre-existing edits and untracked files.
3. Do not edit unrelated feature behavior, change null defaults, bump package versions, commit, or publish unless the active task includes those actions.
4. Read the named files directly. When an implementation location is unknown, follow the repository's semantic-search instructions. Do not rewrite a subsystem based on its filename alone.
5. Make one logical change at a time. Add a meaningful failing regression before a correctness fix. Verify that it fails for the intended reason.
6. Follow the existing TUnit test patterns. Do not introduce another test framework. Do not blindly copy VSTest filter flags: the repository uses Microsoft.Testing.Platform.
7. Never make a test pass by deleting the assertion, ignoring generator diagnostics, suppressing all compiler warnings, or replacing expected output without reviewing the semantic change.
8. Treat all proposed filenames below as suggestions. Reuse an existing appropriate test file if that avoids duplication.

## Step 1 — Record a reproducible baseline

Read:

- `global.json`
- `source/Analyzer.Shared.props`
- `source/AlephSourceGenerator.cs`
- `source/Generation/MapperSourceOutput.cs`
- `source/Generation/MapperGenerationResult.cs`
- `tests/AlephMapper.Tests/SourceGeneratorTests.cs`
- `.github/workflows/build-nuget.yml`

Run these commands separately:

```powershell
git status --short
git rev-parse HEAD
dotnet --info
dotnet test --configuration Release
```

Record test totals, failures, and generated-code warnings. The review observed 224 passing tests; the current checkout may legitimately have a different count. If the baseline fails, identify whether the failure predates your changes. Do not erase or conceal it.

**Checkpoint:** you can explain how an attributed member becomes a mapper result and where source is added to Roslyn. You have recorded the baseline.

## Step 2 — Reproduce IA-01 in the existing test harness

Read `MappingAnalysisFactory.ExtractUsingDirectives`, `AddUsing`, `MapperFileEmitter.Render`, and the harness used by `GenerateAndCompile` in `SemanticSafetyGeneratorTests.cs`.

Add a regression source using `using static System.Math` and an attributed method returning `Abs(value)`. Assert all of the following:

1. The input compilation is valid before generator output is added, excluding references intentionally requiring generated members.
2. A mapping companion is actually generated.
3. The generator reports no crash or unexpected errors.
4. The final compilation has no errors.
5. The expression result agrees with the original method for negative, zero, and positive inputs that avoid `Math.Abs(int.MinValue)` overflow.

Run the regression before changing production code. Expected baseline failure: generated `using System.Math;` causes CS0138. If it already passes, inspect current history and mark the defect as fixed; do not manufacture a failure.

## Step 3 — Fix imports without losing binding

1. Trace how imported names enter the generated signature and rewritten body.
2. Determine which references are already fully qualified and which still depend on imports.
3. Select a coherent strategy: qualify symbol-bound references wherever possible and preserve necessary directive semantics and lexical scope. Merely retaining the words `static` and alias text is insufficient if imports from different declarations are merged into one scope.
4. Do not qualify instance variables or rewrite identifiers by text alone. Use Roslyn symbols to distinguish type names, namespaces, members, and locals.
5. Restrict imported namespace context to the declaration's applicable scopes. Do not collect imports from unrelated namespace siblings.
6. Preserve extension-method binding and LINQ behavior. Removing all imports without handling extension calls is not a valid shortcut.
7. Ensure ordering and generated text remain deterministic.

Add regression cases incrementally:

| Input case | Required observation |
| --- | --- |
| Static import of `System.Math` | Generated code compiles and `Abs` behaves correctly |
| Type alias used in a mapping | Signature/body binds to the intended type |
| Namespace alias | Qualified member access remains valid |
| Alias name reused differently in two partial declarations | Each generated mapping retains its original binding |
| Imports in unrelated namespace siblings | No new ambiguity appears |
| Extension method invoked through an import | The original method binding is preserved |

Only assert supported language features for the relevant compiler band. Use a clear diagnostic for a genuinely unsupported construct rather than emitting invalid C#.

Run the relevant generator tests, then the generator test project using the syntax supported by the current SDK. The solution-wide Release command in Step 1 is the fallback if filtering is uncertain.

**Checkpoint:** the original failing case and the binding/scope cases pass. Review snapshots manually if any change.

## Step 4 — Reproduce and fix IA-02

1. Create two partial containing classes, each with a nested static partial class named `Mapper` and a `[Projectable]` method.
2. Make consumer code call both generated expression factories. This prevents a false pass when a generator warning leaves no output.
3. Assert two distinct generated mapper files, no CS8785, no generator exception, and successful compilation.
4. Confirm the baseline fails because both files request the same hint name.
5. Replace the hint identity algorithm in `MapperFileEmitter.Render` or a focused helper.

Identity requirements:

- Include every containing namespace/type component and type arity.
- Encode separators unambiguously. Replacing every punctuation character with `_` can collide.
- Be deterministic across processes and platforms.
- Do not include absolute source paths, timestamps, random identifiers, or runtime string hash codes.
- If using a digest, use a stable algorithm available to both netstandard2.0 analyzer builds and retain enough digest information to avoid practical collisions.

Add tests for nested same-named mappers, namespaces such as `A.B` versus `A_B`, supported generic containing types with different arities, and identical repeated generation. Test runtime results where two mappings intentionally return different values.

**Checkpoint:** both companions are callable, distinct names remain stable, and no output is silently lost.

## Step 5 — Make cancellation explicit

1. Locate broad exception handling in `MapperSourceOutput.Create` and token propagation into semantic analysis.
2. Ensure a cancellation exception is rethrown before the generic Release failure handler can convert it into AM0004.
3. Pass the existing token into expensive operations where supported. Add cancellation checks at meaningful traversal boundaries, not every trivial string operation.
4. Do not introduce a static/shared cancellation token or a cache that outlives the compilation incorrectly.
5. Add a deterministic cancellation test using the existing generator driver or a focused internal seam. Avoid timing-dependent tests that sleep and hope cancellation occurs.
6. Assert that canceled generation does not produce AM0004. Retain a test that genuine unexpected failures follow the existing failure contract where a reliable injection seam exists.
7. Run both Debug and Release tests because exception handling is conditional on build configuration.

**Checkpoint:** cancellation remains cancellation; real generator failures remain visible. Document any untested branch precisely.

## Step 6 — Establish warning-clean consumer fixtures

1. Revisit the baseline generated warnings and identify their input fixtures.
2. Divide them into unintended generator warnings and intentional unsafe-input/diagnostic cases. Record the reason for each intentional case.
3. Add representative fixtures with nullable analysis enabled and compiler warnings treated as errors.
4. Cover nested null checks, supported switches, extension members, updates, and adaptation output.
5. Fix incorrect annotations/casts/control flow at the generation source. Do not add blanket `#pragma warning disable`, `#nullable disable`, or null-forgiving operators solely to silence tests.
6. Where an annotation is logically justified, prove its precondition in a regression test.
7. Keep intentional warning cases outside the warning-clean contract and assert their expected IDs and relevant locations.

**Checkpoint:** supported safe inputs produce warning-clean generated code; intentional warnings are explicit and bounded.

## Step 7 — Add behavioral equivalence coverage

For each selected supported transformation:

1. Compile the ordinary mapper and its generated expression factory.
2. Compile the expression and execute both implementations on the same inputs.
3. Compare results, including nulls and nested values.
4. Compare exception behavior where relevant and promised by the contract.
5. Use a counter-based helper/getter only when testing evaluation semantics deliberately.
6. Keep known intentional evaluation differences in separate tests with clear names. Do not change existing defaults to make an equivalence test pass.

Begin with null branches, nested helper parameters, precedence, and switch arms. Prefer a small table of meaningful boundary cases over a large number of repeated literals. Property-based testing is optional; introducing a dependency requires a clear benefit.

**Checkpoint:** tests distinguish compilation correctness, semantic equivalence, and intentional divergence.

## Step 8 — Measure incremental performance

1. Reuse the tracked-step infrastructure already present in `SourceGeneratorTests.cs`.
2. Define reproducible fixtures for cold generation, unrelated edits, mapper edits, helper edits, many mappers, and a large single mapper.
3. Separate setup/compilation cost from the operation under measurement.
4. Record SDK/compiler versions, fixture sizes, iterations, elapsed time, allocations where available, and generated output size.
5. Inspect both analysis and output step reasons. Cached output does not prove zero upstream work.
6. Optimize only a measured bottleneck. Retain dependency invalidation when helpers change.
7. Compare before/after results under identical conditions. Avoid fragile CI millisecond thresholds on shared runners.

**Checkpoint:** either a justified optimization is verified or a baseline report explains why no refactor is warranted.

## Step 9 — Final verification and handoff

Run relevant tests after each logical fix. After the final generator change, run the suite and existing package verification. These commands assume the repository root and Python availability:

```powershell
dotnet test --configuration Release
dotnet pack source/AlephMapper.csproj --configuration Release --output artifacts/oss-quality-internal-package
python scripts/verify_roslyn_compatibility.py --package-dir artifacts/oss-quality-internal-package
git diff --check
git status --short
```

The package verification script requires exactly one matching package in the directory. Use a new uniquely named output directory if necessary; do not delete unrelated artifacts. It downloads isolated compiler packages and checks Roslyn 4.8, 4.14, 5.0, and the SDK compiler. If a dependency or network failure blocks it, report that limitation rather than claiming a pass.

Final report must list changed behavior, files, tests run and results, intentional remaining limitations, and status of each IA item. Mark unimplemented items as remaining. Do not describe benchmark proposals as completed benchmarks.
