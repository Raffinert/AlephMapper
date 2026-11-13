# PR #58 structural improvements

The generator still rewrites syntax into readable C# expression lambdas. These changes consolidate responsibilities shared by classic mappings and C# 14 extension members.

- `MapperDeclarationValidator` checks the containing mapper once before generation. Invalid classic declarations receive AM0021; extension requests receive AM0020.
- `GeneratedMemberRegistry` atomically reserves source and generated signatures for Projectable, Updatable and Adapt. `MethodSignature` uses type symbols and C# overload identity: `int` and `int?` remain distinct; nullable reference annotations, tuple element names and method type parameter names do not distinguish signatures. Classic companion collisions receive AM0022; existing extension and adaptation collision diagnostics remain in use.
- `InliningContext` owns shared expansion state and diagnostics. Lambda locals, switch captures and conditional receiver stacks remain local to each visitor.
- `InliningResolver.Arguments` binds classic and C# 14 calls through Roslyn argument operations. `InliningResolver.Expansion` handles child visitor creation, recursion and return conversions in one place, including method-group expansion.
- Static reference handling shares constant expansion, dependency accessibility checks and member qualification. Semantic symbols are resolved before rewriting syntax.

Receiver conversions retain the original semantic receiver where available, avoiding identity casts that obscure adaptation roots. Optional and expanded params arguments are not synthesized. Existing generated-source snapshots remain unchanged.

## Validation

- Release and Debug solution builds and full test runs: 206 passed, 0 failed, 0 skipped in each configuration.
- Added overload identity/hash, atomic reservation, nullable value overload, invalid declaration and factory collision regressions, plus runtime coverage for mixed named/positional classic calls and method-group return conversion.
- Release pack with `--no-build --no-restore` succeeds.
- Actual Roslyn 4.8.0, 4.14.0, 5.0.0 and SDK/Roslyn 5.9 package selection and runtime checks pass on Windows.
- The original workspace and its untracked files are untouched.
