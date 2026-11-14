# Implementation instructions: documentation and contributor experience

Read [docs.md](docs.md) first. This guide covers D-01 through D-07. It does not authorize implementing proposed mapper features, publishing packages, or inventing organizational policies.

This is the baseline implementation plan. Check the assessment's implementation update and current checkout before treating a step as outstanding.

## Working rules

1. Read the current user request and repository instructions.
2. Run `git status --short` and preserve existing edits, especially untracked plans and historical notes.
3. Describe current behavior from source and tests. Treat the assessment's version numbers as a historical baseline until verified.
4. Keep the README short and link deeper material. Avoid copying the same detailed table into several files.
5. Never replace historical release versions globally.
6. Do not document unimplemented strict modes, collection policies, or generic features as available.
7. Use real maintainer contacts and actual supported workflows. Do not invent private security channels, release credentials, response deadlines, or publication guarantees.
8. Run verification appropriate to changed examples. Pure prose edits do not require unrelated new tests.

## Step 1 — Build a documentation inventory

Read these known entry points:

- `README.md`
- `docs/installation-and-compatibility.md`
- `docs/advanced-usage.md`
- `docs/Adapt-Attribute.md`
- `docs/extension-members-compatibility-matrix.md`
- `docs/switch-parity-matrix.md`
- `examples/SampleApp/README.md` and `SampleApp.csproj`
- `.agents/skills/alephmapper/SKILL.md` and its linked references
- `source/Analyzer.Shared.props`
- `source/Attributes.cs`
- `.github/workflows/build-nuget.yml`

Make a small working table with document, audience, current/planned/historical status, version claims, and validation method. Find overlapping statements and assign a canonical home to each detailed contract.

**Checkpoint:** you know which documents users should trust for current behavior and which are historical plans.

## Step 2 — Correct version and sample drift (D-01)

1. Read the current package version from `source/Analyzer.Shared.props`.
2. Decide whether each example describes the current source version, a published package, or a historical release.
3. For published-package claims, verify the release actually exists using an authoritative package source. Do not equate source version with publication.
4. Update current installation examples and skill guidance consistently, or use clearly labeled placeholders where that is the chosen policy.
5. Leave historical release notes unchanged unless correcting a factual error with an explanation.
6. Read `global.json` and the sample project to identify SDK, target framework, and actual language requirements. Do not infer an exact language setting solely from the framework name.
7. Update the sample description and explain its analyzer project reference.
8. Read the sample entry point before running it to understand prerequisites or external effects.

Validate from the repository root:

```powershell
dotnet build examples/SampleApp/SampleApp.csproj --configuration Release
dotnet run --project examples/SampleApp/SampleApp.csproj --configuration Release
```

If the sample requires configuration or external services, document and satisfy the authorized prerequisites instead of inventing output. Include a concise expected result or behavior, not an unstable dump of every console line.

**Checkpoint:** the sample instructions work and all current version claims have an explicit basis.

## Step 3 — Consolidate the generated API reference (D-02)

1. Read attribute declarations and representative generated-output tests.
2. Select one canonical reference location, such as `docs/generated-api-reference.md`.
3. Cover each companion separately: ordinary handwritten method, expression factory, update overload, and explicit adaptation.
4. For each, show minimal input, generated signature, call site, defaults, constraints, and failure boundaries.
5. Verify extra factory parameters, the `dest` parameter name, class/member option precedence, generic constraints, accessibility, and extension-property limitations from the implementation.
6. State null and collection behavior only after verifying current tests or adding focused executable examples.
7. Link the existing adaptation and syntax matrices instead of copying their full contents.
8. Explain ordinary execution, expression construction/compilation, and provider translation as separate stages.
9. Link the reference from README's documentation list.

Compile examples with complete model declarations in fixtures. If a displayed snippet intentionally omits surrounding models for readability, identify the complete runnable example it comes from.

**Checkpoint:** every documented generated signature is called by a compiling example; planned APIs are clearly separated.

## Step 4 — Create the diagnostic catalog (D-03)

1. Enumerate descriptors in `source/Diagnostics/DiagnosticDescriptors.cs` and inspect analyzer release tracking.
2. Include only actual supported IDs. Gaps in numbering are not missing documentation to invent.
3. Create a stable anchor or page for each descriptor.
4. Record ID, title, default severity, triggering input, explanation, corrected example, and companion-generation consequence.
5. Check the actual diagnostic location and behavior in tests.
6. Explain that suppressing a missing-companion warning does not generate the companion.
7. Add relevant links from troubleshooting and the API reference.
8. If editing `helpLinkUri`, preserve the existing useful crash-report workflow and verify the target documentation URL will exist in the release containing the change.
9. Run relevant diagnostic tests when descriptors are changed; prose-only additions need example/link validation.

**Checkpoint:** catalog coverage matches the current descriptor inventory, and links are stable and usable.

## Step 5 — Write contributor and architecture guidance (D-04)

Create `CONTRIBUTING.md` if an equivalent current guide does not exist. Include:

1. Required SDK and supporting Python requirement for compiler/package verification.
2. Restore, build, and test commands taken from the actual workflow.
3. The Microsoft.Testing.Platform runner and a verified approach to focused test execution.
4. Repository layout and the generator's discovery, analysis, rewriting, and emission responsibilities.
5. How to add a minimal generator regression and assert final compilation.
6. When to add runtime or EF provider tests.
7. How to inspect generated source using the documented generated-file settings.
8. How both Roslyn analyzer bands are built and tested.
9. How to report a bug with compiler version, provider version, minimal source, and diagnostics.

Create a short architecture document if needed. Ground it in `AlephSourceGenerator`, `MapperSourceOutput`, the analysis models, rewriters, emitters, and result models. A small Mermaid flow is appropriate. Clearly distinguish compiler validation from database-provider translation.

Use real commands, for example:

```powershell
dotnet restore AlephMapper.slnx
dotnet build AlephMapper.slnx --configuration Release --no-restore
dotnet test AlephMapper.slnx --configuration Release --no-build --no-restore
dotnet pack source/AlephMapper.csproj --configuration Release --output artifacts/oss-quality-docs-package
python scripts/verify_roslyn_compatibility.py --package-dir artifacts/oss-quality-docs-package
```

Explain that the verifier expects exactly one matching package and may download compiler packages. Choose a fresh output directory when necessary.

**Checkpoint:** a contributor can build, test, reproduce a defect, and understand where a fix belongs using the guide alone.

## Step 6 — Document release and support policy (D-04/D-05)

1. Read the actual workflow and distinguish artifact upload from package publication.
2. Locate any existing release process before proposing another one.
3. Write a checklist covering version metadata, current docs/skill examples, release notes, API compatibility, compiler/package checks, sample execution, and artifact verification.
4. State only tested compiler/provider combinations as verified support.
5. Describe proposed compatibility/deprecation policy as proposed until it is adopted.
6. Add support and bug-report guidance using real existing channels.
7. For security reporting or a code of conduct, verify the maintainer-supported contact and enforcement owner. If missing, document the precise outstanding decision instead of publishing a fictitious policy.

Do not publish packages or create external messages as part of writing this guide. A completed release checklist is a document, not evidence that a release was performed.

**Checkpoint:** the release process is accurate, and unresolved governance decisions are explicitly identified rather than fabricated.

## Step 7 — Make important documentation executable (D-06)

1. Select the key user journeys: quick-start projection, helper composition, factory parameters, predicate, update, adaptation, and C# 14 extension member.
2. Give each snippet a stable identifier and map it to a complete compiling fixture.
3. Choose one simple synchronization method: extract marked snippets into fixtures, or generate displayed snippets from tested source. Avoid two independent copies that can drift silently.
4. Include model definitions and dependencies required for compilation.
5. Compile classic examples in the appropriate supported compiler band; put C# 14 examples only in supported hosts.
6. Execute examples where runtime behavior matters. Use real SQLite integration tests for SQL claims.
7. Keep historical plans and intentionally failing diagnostic examples out of the success-only snippet set. Validate expected failures separately.
8. Add an explicit CI step after local validation succeeds.

For link checking:

1. Resolve relative paths from each Markdown file's directory.
2. Validate fragment anchors as well as file existence.
3. Handle fenced code blocks and intentional example URLs appropriately.
4. Separate deterministic local checks from external network checks, which may fail transiently.
5. Test the packaged README context; use absolute repository links or a documented transformation when NuGet cannot resolve relative links correctly.

**Checkpoint:** changing a critical example incompatibly causes CI to fail, and documentation navigation works in both repository and package contexts.

## Step 8 — Improve package presentation and verify provenance (D-07)

1. Read current package metadata in `source/AlephMapper.csproj`.
2. Write a concise description and tags that match actual capabilities.
3. Pack to an isolated artifact directory and inspect the `.nupkg` and any symbols artifacts.
4. Check repository metadata, source revision information, PDB/symbol availability, and Source Link using suitable tooling.
5. Do not assume an explicit Source Link package is required; inspect SDK-produced metadata first.
6. If testing reproducibility, rebuild with the same inputs and build properties. Account for build numbers and time-dependent metadata before comparing outputs.
7. Record what was verified and what remains unverified. Do not claim reproducibility based solely on a project property.

Changes to package content or build properties require the existing package compatibility checks. Documentation describing provenance must reflect actual artifact evidence.

**Checkpoint:** metadata is accurate and provenance claims are backed by inspected artifacts.

## Step 9 — Final quality check and report

1. Read the edited documents as a new user would, starting from README.
2. Check every newly added local link and important external link.
3. Run the documented example/sample commands affected by the edits.
4. Run descriptor tests for diagnostic-code changes and package checks for packaging changes.
5. Run `git diff --check` and inspect `git diff --stat` and `git status --short`.
6. Confirm unrelated historical drafts and pre-existing untracked files were preserved.

Final report must list documents added/updated, examples executed, link checks performed, version assumptions, any code/package changes, and unresolved policy decisions. Do not report an unrun command as verified.
