# Switch lowering compared with ExpressiveSharp

Checked 2026-11-03 against ExpressiveSharp main at [`462edb54b2012c4d55f4b603d4a085720a091dc8`](https://github.com/EFNext/ExpressiveSharp/tree/462edb54b2012c4d55f4b603d4a085720a091dc8) and AlephMapper PR #58 (`65b8605` plus the tuple signature normalization fix identified below).

The common subset matches the same conditional-tree design. This is not complete feature or behavioral parity, and upstream edge-case behavior should not become AlephMapper's specification.

## Design and supported subset

AlephMapper rewrites bound syntax to readable C# lambdas. ExpressiveSharp's operation-based emitter writes `Expression.Condition`, `AndAlso`, `TypeIs`, `Convert`, member accesses and other expression factory calls. The resulting switch trees have broadly the same shape: reverse-folded arms, first matching arm wins, guards combined with short-circuit AND.

Both support the tested scalar constants, relational/logical patterns, ordinary type captures and guards, property patterns, nullable comparisons and nested switches. Both can repeat evaluation of governing expressions. Reusing an expression node in an expression tree does not cache its runtime value.

Deliberate differences:

- AlephMapper requires a final unguarded discard arm, including for switches that are otherwise exhaustive. Missing fallback receives AM0019. ExpressiveSharp emits `Default(resultType)` when no discard exists; for an integer result this is zero, unlike C#'s unmatched-switch exception.
- AlephMapper verifies rank-one arrays and exact `List<T>` list shapes. ExpressiveSharp additionally discovers Count/Length and integer indexers on other types. That broader acceptance is not proof of equivalent semantics.
- AlephMapper positional lowering requires source-verifiable synthesized record deconstruction and positional properties. ExpressiveSharp also resolves tuple ItemN fields and approximates custom deconstruction through case-insensitive parameter/property name matching; it does not invoke the selected Deconstruct method in this path.
- AlephMapper rejects captured slices, general boxed scalar comparisons, NaN constants and `is` captures requiring a wider scope. These are explicit AM0019 limitations rather than full language support.
- AlephMapper preserves ordinary C# switches in update-only paths. This comparison concerns generated expression companions.

Implementation references: [upstream patterns](https://github.com/EFNext/ExpressiveSharp/blob/462edb54b2012c4d55f4b603d4a085720a091dc8/src/ExpressiveSharp.Generator/Emitter/ExpressionTreeEmitter.cs#L1629), [upstream list lowering](https://github.com/EFNext/ExpressiveSharp/blob/462edb54b2012c4d55f4b603d4a085720a091dc8/src/ExpressiveSharp.Generator/Emitter/ExpressionTreeEmitter.cs#L1785), [upstream switch lowering](https://github.com/EFNext/ExpressiveSharp/blob/462edb54b2012c4d55f4b603d4a085720a091dc8/src/ExpressiveSharp.Generator/Emitter/ExpressionTreeEmitter.cs#L2194), [AlephMapper lowering](../source/SyntaxRewriters/InliningResolver.SwitchExpressionRewriter.cs), [existing coverage matrix](switch-parity-matrix.md).

## Runtime differential probes

Built the upstream generator from the pinned checkout. An isolated Roslyn 5.9 harness ran each generator on equivalent annotated static mapping methods, compiled the generated expression factories, and compared their compiled delegates with the original C# methods. The upstream registry source was excluded from the probe compilation; the actual generated member factory bodies were retained and invoked. These probes test expression construction and in-memory execution, not SQL translation or upstream runtime registry integration.

Sixteen cases ran against both generators (32 executions):

| Case / input | Original C# | ExpressiveSharp expression | AlephMapper expression |
|---|---|---|---|
| `>= 90`, input 95 | 1 | 1 | 1 |
| `>= 10 and < 20`, input 15 | 1 | 1 | 1 |
| `string text when text.Length > 1`, input `"abc"` | 1 | 1 | 1 |
| `{ Length: > 1 }`, input `"abc"` | 1 | 1 | 1 |
| Nullable `> 0`, input null | 0 | 0 | 0 |
| Nested switch, input 2 | 2 | 2 | 2 |
| Incrementing `Next() switch { 0 => 0, 2 => 2, _ => 3 }`, counter initially 0 | 3 | 2 | 2 |
| `{ Length: var n } when n > 0 => n`, input `"abc"` | 3 | InvalidOperationException | 3 |
| `[var n] when n > 0 => n`, input `[7]` | 7 | InvalidOperationException | 7 |
| `value switch { 1 => 1 }`, input 2 | SwitchExpressionException | 0 | Skipped, AM0019 |
| `[1, .., 3]`, input `[1,9,3]` | 1 | 0 | 1 |
| `[1]`, input null | 0 | NullReferenceException | 0 |
| `var n when n == null`, input null | 1 | 0 | 1 |
| `double.NaN` constant, input NaN | 1 | 0 | Skipped, AM0019 |
| `_ when value > 0 => 1, _ => 0`, input -1 | 0 | 1 | 0 |
| Tuple `(1,2)` pattern, input `(1,2)` | 1 | 1 | Skipped, AM0019 after fix below |

All unspecified switch fallbacks in the table are `_ => 0`.

Source inspection explains the upstream discrepancies: list lowering omits a null guard and uses forward element indices even after a slice; declaration binding only handles a top-level declaration; discard-arm selection ignores its guard; constants use ordinary equality, including NaN. These observations apply to the pinned revision and the tested entry point, not every ExpressiveSharp API or future version.

## AlephMapper regression found and fixed

The first tuple probe reported AM0004 rather than the documented AM0019. The new signature registry assumed every Roslyn tuple symbol has a non-null `TupleUnderlyingType`. Some unnamed/underlying ValueTuple symbols do not. Normalization now retains the original symbol when that property is null.

Added regressions cover unnamed tuple versus named tuple identity, explicit `System.ValueTuple<int,int>` identity, and unsupported tuple patterns producing AM0019 while a sibling mapping still generates. Tuple pattern support remains deferred.

Release and Debug solution tests after the fix: **209 passed, 0 failed, 0 skipped** in each configuration. Release pack and actual Roslyn 4.8, 4.14, 5.0 and SDK/Roslyn 5.9 package-selection/runtime checks also pass. Existing pattern semantics and generated-source snapshots remain unchanged.

## Follow-up priorities

1. Preserve the current fallback, null, capture and suffix behavior; do not copy upstream discrepancies for parity.
2. Tuple positional patterns are a useful contained coverage gap: verified ValueTuple field access can be added without approximating custom Deconstruct behavior.
3. Consider dedicated NaN lowering separately, with explicit provider-translation tests.
4. Widen custom indexable/deconstruction support only when the selected bound operations can be preserved and verified.
