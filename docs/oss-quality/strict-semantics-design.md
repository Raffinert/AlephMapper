# F-01 design: opt-in strict transformation checks

**Status: design only.** No strict option or `AM0024` diagnostic is shipped in 0.8.3. The current default behavior, including `NullConditionalRewrite.Ignore`, remains unchanged. This proposal defines a conservative first increment for a later compatible release; an API name and public release decision are still required.

## Contract and scope

The proposed opt-in check would cover generated expression factories from `[Projectable]` and generated map/expression members from `[Adapt]`. A requested adapted pair would be omitted together if its inlined body fails the check. Handwritten methods keep ordinary C# execution. Generated update overloads are outside the first increment because their assignment/collection semantics require a separate equivalence contract; enabling the option would not imply update safety. The option must be documented as narrower than general program equivalence and narrower than provider translation.

For covered members, the guarantee would be: substitutions and lowering must not duplicate, omit, or reorder evaluation of an expression whose evaluation may be observable, relative to one invocation of the handwritten method with equivalent parameter values. A literal, compile-time constant, or direct read of a method parameter can be repeated or omitted. A property getter, indexer, method call, assignment, increment, object creation, event access, or unknown operation is treated as observable unless a future design specifies stronger evidence. Reading a field on an object is also treated as potentially observable when its receiver can change. The checker does not claim a general purity proof.

Extra expression-factory arguments are evaluated by C# when the factory is called, not once per query row. The guarantee applies to the resulting lambda body after those values are captured. User-supplied argument expressions at the factory call site and effects inside database providers are outside the generator's control.

## Decision table for the first increment

| Source shape | Proposed strict result | Reason |
| --- | --- | --- |
| `Twice(3)` where `Twice(x) => x + x` | Generate | Repeating a literal has no observable evaluation. |
| `Twice(value)` where `value` is a method parameter | Generate | Repeating a direct parameter read has no observable evaluation. |
| `Twice(Next())` | Warn and omit covered companion | Inlining evaluates `Next()` twice. |
| `First(Next(), SideEffect())` where helper ignores its second argument | Warn and omit | Inlining would skip an observable argument. |
| Helper swaps two `Next()` arguments | Warn and omit | Inlining changes observable order. |
| `Next() switch { > 0 and < 10 => 1, _ => 0 }` | Warn and omit if lowered input is visited more than once | Current lowering can repeat the call. A single-use lowering remains eligible. |
| `item.Property switch { ... }` | Warn and omit if lowered input is repeated | A property getter may be observable. |
| `obj?.Member` with strict + `Ignore` | Warn and omit | Removing `?.` changes null behavior. |
| `obj?.Member` with strict + `Rewrite` | Generate only when the receiver is a direct parameter/constant or can otherwise be proven single-evaluation | A property path may invoke getters twice. Existing `AM0016` already rejects some unstable receivers. |
| `obj?.Member` with strict + `None` | Existing `AM0017`; omit expression | `?.` cannot remain in an expression tree. |

The proposed diagnostic is a warning at the original argument, receiver, or switch expression with a concise cause and recovery action. `AM0024` is a proposed next ID, subject to descriptor/release-catalog review; it is not allocated in source. Suppression would hide the warning but would not create the omitted companion. The checker would run before signature reservation and emission so unaffected siblings can still generate.

## Configuration and precedence proposal

The future setting would be opt-in on `[Projectable]` class or member declarations, with the member setting taking precedence. Each `[Adapt]` declaration would carry its own explicit setting; it would not inherit the `[Projectable]` setting because adaptations can have different source types and null strategies. For a C# 14 extension member, the member setting would take precedence over the outer class setting. No new setting would change current behavior when absent.

`Ignore`, `Rewrite`, and `None` remain independently selectable for null-conditionals. Strict checking would reject transformations that violate its evaluation guarantee under the selected null strategy; it would not silently switch strategies. The checker should operate on semantic symbols and usage counts, not textual occurrence counts, and should account for conditional branches where an argument may be skipped.

The output must remain an ordinary `Expression<Func<...>>` using the existing provider-friendly expression shapes. Do not inject expression blocks or temporary variables merely to preserve one evaluation. A provider must still validate translation independently, as the [SQLite evidence](../sqlite-translation-evidence.md) demonstrates only selected shapes.

## Implementation gate

Before coding, maintainers must approve the public option name and whether the first increment should also cover update overloads. Tests must include each decision-table row, class/member precedence, separate `[Adapt]` declarations, C# 14 extension members on the Roslyn 5.0 asset, default compatibility, diagnostic span/omission, ordinary-vs-generated execution for accepted cases, and SQLite translation for any new emitted shape. The package compiler matrix must pass on the supported Roslyn bands. A safer default null policy is a separate breaking-release decision.
