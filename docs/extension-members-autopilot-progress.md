# Extension members autopilot progress

- Starting main: `3fcc1a76b1bec0f4f6eacda49add93c8879e0350` (unchanged).
- PR #56 verified open/mergeable, CI run 41683392210 successful, all review threads resolved.
- Created dev_v0_8_0 from main, retargeted #56, squash-merged 2026-11-03 to `d154572081ba338c790f63f17605a98392dbe333`.
- Feature `feat/extension-members-parity-v0.8.0` created from that integration head in isolated worktree C:/Play/AlephMapper-extension-members. Owner untracked files untouched.
- Integration baseline: restore and Release build successful, 157 tests passed, 0 failed/skipped. Existing generated nullable warnings remain.
- SDK 10.0.403 hosts Roslyn 5.9.0. Independent net10.0 probe compiled against Roslyn 5.0.0 verifies CSharp14, ExtensionBlockDeclarationSyntax, IsExtension, ExtensionParameter, original symbols and consumer calls. Instance receiver is separate from ordinary method parameters. Generator minimum upgraded to 5.0.0 (netstandard2.0 retained).
- API: outer static class MemberExpression(extraArguments) returning Expression<Func<Receiver, Result>>. Properties use zero-argument factory. Extension properties receive Projectable support only.
- New PR MUST target dev_v0_8_0 and remain unmerged pending owner approval. No releases/tags/publishing.
