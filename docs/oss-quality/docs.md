# OSS quality assessment: documentation and contributor experience

Assessment baseline: commit `a40c4b5`, reviewed 2026-11-13. Implementation target: package version `0.8.3`.

Implementation update for this branch: D-01 version and sample guidance, D-02 generated API reference, D-03 diagnostic catalog, D-04 contributor/release guidance, and D-06 runnable documentation and link checks are in place. D-05 still needs a maintainer-supported confidential security route and governance decisions. D-07 improves package description and tags; symbol distribution and reproducibility remain unverified. The sections below retain the original assessment and acceptance criteria.

Companion implementation guide: [docs-agent-instructions.md](docs-agent-instructions.md).

Related assessments: [internal API and design](internal-api.md) and [features](features.md).

## Current strengths

The root README gives a concise installation path and examples for projections, updates, adaptations, composition, modern syntax, and null handling. It links to advanced guides and explicitly notes provider translation limits. Preserve that structure; the README need not contain the entire API reference.

The repository already has compatibility matrices, an adaptation reference, troubleshooting guidance, a sample application, and a portable agent skill. Improve their consistency and verification rather than duplicating them into competing sources of truth.

## D-01 — P1: eliminate current documentation drift

The review found version drift. The current version alignment target is:

- `source/Analyzer.Shared.props` declares package version `0.8.3`.
- `docs/installation-and-compatibility.md` uses `0.8.3` in package-reference examples.
- `.agents/skills/alephmapper/SKILL.md` describes `0.8.3`.
- `examples/SampleApp/README.md` describes C# 13 and .NET 9, whereas `SampleApp.csproj` targets .NET 10.

Choose a clear policy for versioned examples: synchronized explicit versions or clearly marked placeholders. Historical release notes should retain their historical versions. Distinguish a version declared in the source from a version actually published to NuGet.

Add exact sample build/run commands, the required SDK, expected observable behavior, and an explanation that the sample consumes the generator source project.

## D-02 — P1: publish the generated API contract

Create or consolidate a reference covering:

- Attribute targets, defaults, and class/member precedence.
- Generated member names, accessibility, parameters, return types, and generic constraints.
- Additional parameters becoming expression-factory parameters.
- Null source and null destination behavior for each companion kind.
- Collection skip/update semantics and entity-tracking implications.
- Classic methods versus C# 14 extension methods/properties.
- Supported adaptation shapes and compiler-validation boundaries.
- The distinction between ordinary method semantics, generated expression semantics, and SQL translation.

Use minimal input/output examples validated by compilation. Link detailed matrices rather than duplicating their complete contents. Do not document a proposed strict mode or generic feature as shipped.

## D-03 — P1: publish a diagnostic catalog

Give every currently supported `AMxxxx` diagnostic a stable page or anchor containing ID, title, severity, cause, example, correction, and whether a companion is omitted. Describe suppression consequences: hiding a warning does not create missing generated members.

Connect `helpLinkUri` to documentation where appropriate. Preserve the crash-report path's usefulness. Do not invent entries for gaps in diagnostic numbering or reassign IDs without a compatibility decision.

Source of truth: `source/Diagnostics/DiagnosticDescriptors.cs` and the analyzer release files.

## D-04 — P1: document how to contribute and release

Add a contributor guide with prerequisites, exact build/test commands, repository layout, analysis/emission boundaries, how to reproduce a generator defect, test conventions, and package verification.

Document compiler support, provider claims, generated API stability, diagnostic stability, deprecation, and breaking-change policy. Provide an architecture overview showing discovery → analysis → rewriting → emission → compiler/provider validation.

Describe the existing CI accurately: it builds, tests, packs, verifies compiler compatibility, smoke-tests package consumption, and uploads artifacts. The reviewed workflow does not itself publish to NuGet. Record the actual release process; do not invent credentials, release automation, or maintainer guarantees.

A release checklist should include version alignment, release notes, generated API compatibility, compiler/package checks, sample validation, and artifact provenance checks.

## D-05 — P2: document maintenance and support

Provide security-reporting guidance using a real maintainer-supported route, support/issue instructions, and concise issue templates where useful. A code of conduct is a governance decision and needs real enforcement ownership; do not fill in invented contacts.

Clarify supported versions and distinguish verified combinations from untested ones. Define how users should submit minimal reproductions, compiler versions, generated diagnostics, and provider information.

## D-06 — P1: keep examples executable

Compile important documentation examples in CI and run the main sample. Use a small, explicit manifest or fixture mapping so each example has a clear source and test. Cover projections, extra parameters, predicates, updates, adaptations, and C# 14 extension members in the appropriate compiler band.

Validate relative links and fragment anchors. Check package README rendering separately: relative repository links do not necessarily resolve correctly on NuGet. Use suitable absolute links in packaged documentation or a deliberate packaging transformation.

Do not turn historical drafts, progress reports, or design proposals into runnable user recipes. Label or index these separately when they are retained.

## D-07 — P2: improve package discoverability and traceability

The package description is currently “Your manual mapping companion” and tags are sparse. Describe the actual source-generator, projection, and update capabilities without claiming universal provider translation.

Verify the produced package for repository metadata, symbol/debug information, Source Link, and reproducibility. Modern SDKs may already supply some metadata; inspect artifacts before claiming a missing capability. Document the verified result and any remaining work.

Reference: [Microsoft library guidance on Source Link](https://learn.microsoft.com/dotnet/standard/library-guidance/sourcelink).

## Recommended sequence and acceptance criteria

1. Correct D-01 and validate the sample.
2. Consolidate the API and diagnostic references.
3. Add contributor and release guidance.
4. Introduce executable documentation and link checks.
5. Complete maintenance policy and package-presentation improvements with verified facts.

Documentation is complete when its examples compile, links resolve, version claims match their stated scope, new contributors can reproduce the build, and users can distinguish supported behavior from planned features.
