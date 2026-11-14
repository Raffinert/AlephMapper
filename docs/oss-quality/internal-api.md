# OSS quality assessment: internal API and design

Assessment baseline: commit `a40c4b5`, reviewed 2026-11-13. Implementation target: package version `0.8.3`. Recheck the current checkout before implementing this assessment.

Implementation update for this branch: IA-01 import binding and scope, IA-02 generated file identity, and IA-03 cancellation have fixes and regression tests. IA-04 has warning-clean consumer fixtures, while existing nullable receiver warnings from some rewrites remain visible. IA-05 includes targeted runtime and package-consumer regressions. IA-06 has an initial [generator benchmark](../../benchmarks/GeneratorBenchmark/README.md), without a CI performance threshold. The sections below preserve the original assessment and acceptance criteria; they do not describe the current generator state.

Companion implementation guide: [internal-api-agent-instructions.md](internal-api-agent-instructions.md).

Related assessments: [features](features.md) and [documentation](docs.md).

## Scope and evidence

“Internal API” here means generator architecture, internal models, semantic rewriting, generated-source identity, diagnostics, cancellation, and testing boundaries. It does not imply that internal types should become public.

The baseline Release test run passed **224 tests, with no failures or skips**. It emitted nullable warnings in generated integration-test code. Two additional consumer builds outside the repository reproduced the defects below. Passing tests therefore do not establish complete generated-code correctness.

Priority definitions:

- **P0:** confirmed correctness defect; fix before expanding functionality.
- **P1:** reliability work for a stable library contract.
- **P2:** measured optimization or later developer-experience work.

## Strengths to preserve

- Ordinary handwritten C# is the mapping source of truth.
- `source/AlephSourceGenerator.cs` uses attribute-based incremental discovery and separate source/diagnostic output paths.
- `source/Generation/MapperGenerationResult.cs` provides a value-based boundary between semantic analysis and incremental output.
- Analysis, generation contexts, and member/file emitters are separated.
- Substitution in `source/SyntaxRewriters/InliningResolver.InvocationRewriter.cs` uses symbols and respects expression precedence.
- Primary-candidate selection precedes complete mapper analysis. Do not claim that every attribute performs a complete duplicate analysis without measurements.
- Tests already cover incremental stability, unsupported syntax, generics, and semantic regressions.
- CI already packs and consumes the NuGet package and checks actual compiler versions. This infrastructure should be extended rather than duplicated.

## IA-01 — P0: preserve import semantics and scope

**Status: confirmed defect for static imports; related alias/scope risks follow from the implementation.**

`MappingAnalysisFactory.ExtractUsingDirectives` collects directive names as strings. `AddUsing` discards `static` and alias syntax. It also collects namespace imports across the compilation unit. `MapperFileEmitter.Render` reconstructs these strings as ordinary `using` directives.

Reproduction:

```csharp
using AlephMapper;
using static System.Math;

public static partial class Mapper
{
    [Projectable]
    public static int Map(int value) => Abs(value);
}
```

Generated output includes `using System.Math;`, producing **CS0138**.

Improve symbol qualification and import handling together. Preserving directive text alone does not solve aliases with the same name in different partial declarations or namespace scopes. Prefer fully qualified bound references where feasible; retain the imports that are genuinely necessary with correct scope and meaning.

Acceptance criteria:

- Static imports, type aliases, namespace aliases, and namespace-local imports compile and retain their intended bindings.
- Two partial declarations can use the same alias name for different types without corrupting generated output.
- Unrelated namespaces in the same input file cannot introduce ambiguity into the mapper.
- Generated output remains deterministic.

## IA-02 — P0: make generated file identities unique

**Status: confirmed generator failure.**

`MapperFileEmitter.Render` derives a hint name from the namespace and innermost type name. Containing types and generic arity are absent. Two supported nested mappers named `First.Mapper` and `Second.Mapper` therefore both request `Mapper_GeneratedMappings.g.cs`.

A real consumer build produced **CS8785** with an `ArgumentException` about the duplicate hint name. Roslyn reported that the generator would contribute no output. A consumer that never calls the generated APIs may still build with this warning; that is not successful generation.

Use a deterministic identity derived from the complete namespace and containing-type metadata path, including generic arity. Sanitization must not turn distinct names into the same identifier. Use an injective encoding or a readable prefix plus a deterministic cryptographic digest; do not use process-dependent `GetHashCode()` values.

Acceptance criteria:

- Same-named nested mappers generate separate files and callable companions.
- Namespace punctuation/underscore combinations do not collide.
- Supported generic containing types remain distinct.
- Repeated identical builds produce identical hint names.

## IA-03 — P1: respect cancellation and isolate failures

**Status: code-level risk, not a reproduced cancellation failure.**

The Release branch of `MapperSourceOutput.Create` catches `Exception`. Cancellation must propagate rather than becoming `AM0004`. Long semantic traversals and recursive expansion should observe cancellation without introducing global mutable state.

Keep unexpected failures actionable: report the relevant mapper location when available and preserve useful debugging information. Do not hide generator defects behind successful empty output. Remember that exceptions from `AddSource` occur outside this analysis catch; correct identities prevent the reproduced collision.

Acceptance criteria: canceled work does not emit a crash diagnostic; genuine unexpected failures retain the established error contract; Debug and Release behavior are both deliberately tested.

## IA-04 — P1: establish warning-clean generated-code contracts

**Status: nullable warnings observed during the baseline build.**

Create representative consumer fixtures with nullable analysis and warnings-as-errors. Classify warnings before changing generation: some fixtures may intentionally exercise unsafe or diagnostic-producing input. Assert intentional warnings explicitly rather than globally suppressing nullable analysis.

Cover nullable nested members, switches, extension methods/properties, updates, and adaptations. Account for the documented use of `#nullable restore`; changing source-local nullable behavior is a contract decision.

## IA-05 — P1: verify behavior as well as source shape

Add focused equivalence tests between ordinary mappings and compiled expression companions over supported, stable inputs. Compare values, null behavior, exceptions, and evaluation counts where the contract promises equivalence.

Keep intentional semantic differences explicit. Existing switch/helper substitution may duplicate, omit, or reorder evaluation; do not silently redefine those cases as equivalent. The feature assessment covers a prospective strict mode.

Use compilation assertions alongside behavioral assertions. A snapshot alone cannot prove binding or runtime behavior. Add regression tests for both P0 defects through the existing generator harness and package-consumer coverage where appropriate.

## IA-06 — P2: measure before refactoring incrementality

The architecture already has value equality and incremental tests. Add benchmarks for cold generation, unrelated edits, one mapping edit, a shared-helper edit, many mapper classes, and a large single mapper.

Measure analysis time, allocations, generated source size, and tracked step reasons. Distinguish “unchanged generated output” from “analysis did not rerun.” Investigate candidate enumeration and shared-helper expansion only if measurements identify them as meaningful costs.

Do not start with a wholesale intermediate-representation rewrite. Introduce smaller models or caches only with a demonstrated correctness or performance benefit and correct invalidation behavior.

## Release gate and sequencing

1. Reproduce and fix IA-01 and IA-02 with regression tests.
2. Address IA-03 and establish IA-04 fixtures.
3. Extend IA-05 around the most failure-prone transformations.
4. Measure IA-06 and choose optimizations from evidence.
5. Run the existing compiler/package matrix after generator changes.

References: [Roslyn incremental generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md), repository `scripts/verify_roslyn_compatibility.py`, and `.github/workflows/build-nuget.yml`.
