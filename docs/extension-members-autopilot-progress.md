# Extension members autopilot progress

- Starting main: `3fcc1a76b1bec0f4f6eacda49add93c8879e0350` (unchanged).
- PR #56 verified open/mergeable, CI run 41683392210 successful, all review threads resolved.
- Created dev_v0_8_0 from main, retargeted #56, squash-merged 2026-11-03 to `d154572081ba338c790f63f17605a98392dbe333`.
- Feature `feat/extension-members-parity-v0.8.0` created from that integration head in isolated worktree C:/Play/AlephMapper-extension-members. Owner untracked files untouched.
- Integration baseline: restore and Release build successful, 157 tests passed, 0 failed/skipped. Existing generated nullable warnings remain.
- SDK 10.0.403 hosts Roslyn 5.9.0. Independent net10.0 probe compiled against Roslyn 5.0.0 verifies CSharp14, ExtensionBlockDeclarationSyntax, IsExtension, ExtensionParameter, original symbols and consumer calls. Instance receiver is separate from ordinary method parameters. Generator minimum upgraded to 5.0.0 (netstandard2.0 retained).
- API: outer static class MemberExpression(extraArguments) returning Expression<Func<Receiver, Result>>. Properties use zero-argument factory. Extension properties receive Projectable support only.
- New PR MUST target dev_v0_8_0 and remain unmerged pending owner approval. No releases/tags/publishing.

## Implementation and review

- E00: 2ea9211, executable Roslyn probe; baseline158 including probe; package smoke passed.
- E01-E04: 7bebd06, discovery, separate receiver/member metadata, property catalog, transitive expansion and AM0021. Expanded receiver/cycle/determinism suite173 passed.
- E05-E07: b19767c, conversions, lambda alpha-renaming, static dependencies, escaped names and explicit unsupported forms. Initial bound-methodgroup negative fixture incorrectly used a value-type receiver (invalid CSharp delegate source); corrected to valid string receiver in f9f3f50, preserving assertions.
- E08: f9f3f50, nonfinite constants, qualified accessibility, query rejection and partial-class failure isolation. Full186 passed.
- Three bounded independent review cycles: all reported capture, static binding/accessibility, escaped name, conditional conversion, constants and query-capture findings fixed. Final cycle found nameof substitution; original INameOfOperation is now folded before substitution with runtime regression. No known findings remain; newest nameof fix has test evidence, not a fourth review.
- Only Projectable AttributeUsage changed in25 attribute snapshots. Existing generated mapper snapshots retained.
- Release/Debug builds and fresh packaged net10/CSharp14 and net8/classic consumers verified. Existing nullable warnings remain visible.
- New PR must target dev_v0_8_0 and remain OPEN pending owner approval; no auto-merge/release/tag/publication.

## Final local verification

- `dotnet restore AlephMapper.slnx`: pass.
- `dotnet build AlephMapper.slnx -c Release --no-restore`: pass, zero errors; existing nullable warnings visible.
- `dotnet build AlephMapper.slnx -c Debug --no-restore`: pass, zero errors; nullable warnings visible.
- `dotnet test AlephMapper.slnx -c Release --no-build --no-restore -- --report-trx --results-directory artifacts/test-results`: 191 passed, 0 failed, 0 skipped.
- `dotnet pack source/AlephMapper.csproj -c Release --no-restore -o artifacts/nuget`: pass, local0.7.4 artifact only (version unchanged, no publication).
- Fresh isolated package caches: CSharp14/net10 consumer restores and executes property/method/combined expression comparisons; classic CSharp12/net8 consumer restores and executes. No CS9057 or analyzer assembly-load failures. Both use the final packed analyzer without direct Roslyn references.
- `git diff --check`: pass.
- Final review fix commit: 0f7f516 (nameof). No known unresolved findings after three bounded review cycles.
- Main remains3fcc1a76b1bec0f4f6eacda49add93c8879e0350; integration remainsd154572081ba338c790f63f17605a98392dbe333.

Owner gate: extension-members PR has NOT been merged. Await explicit approval to merge into dev_v0_8_0. CI and remote review status are reported in the PR and final handoff separately from local checks.

## GitHub PR review follow-up

PR57 targets dev_v0_8_0 and remains open. CI run41689329495 passed on713c68d.
The first remote review found two P2 issues beyond the three local review cycles:
classic-lambda nameof constants after alpha-renaming, and unqualified generic static
dependencies. Both reproduced in Classic_lambda_renaming_preserves_nameof_and_generic_static_binding;
fixed original nameof folding for any substitution scope and qualified generic
static methods by their bound containing type. Added qualified/unqualified private
generic dependency negatives. Full189 passed; Release/Debug, pack and fresh net10
and net8 package-consumer programs rechecked successfully. Requested remote review
of the corrected head; final CI status is recorded on the PR.

Remote review cycle2: preserved inferred anonymous-object member names from bound
IAnonymousObjectCreationOperation properties when alpha-renaming expressions;
added runtime compilation regression (result4). Root nameof folding now runs for
expression projections too, while retaining ordinary Update output formatting.
The review's nameof(extensionProperty) source is itself rejected by CSharp14 with
CS9316; a dedicated original-compilation regression records that restriction.
Valid root nameof(ordinaryProperty) also has runtime coverage. Full191 passed;
Release/Debug, pack and fresh net10/net8 consumers passed again. Final remote
review cycle3 requested; no further automatic cycles will be started.
