# Switch pattern parity

Baseline: branch `fix/switch-semantics-symbol-inlining-20261101`, HEAD `5c78b723a459f11eeffe1c7642636951f94a0286`, .NET SDK 10.0.403. Restore and Release build succeeded (two existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.6). Full Release suite: 125 passed, 0 failed, 0 skipped.

S01 introduces a structured lowering result carrying symbol-identity bindings for the current matched path. Existing declaration/parenthesized/name-collision runtime regressions in `SemanticRegressionTests` cover preserved behavior. Additional pattern support and its evidence are recorded below as implemented.

## Switch pattern parity within documented syntax-rewriter constraints

This is tested syntax coverage, not a claim of complete ExpressiveSharp parity.
Original bound Roslyn operations inform readable C# syntax generation; expression
factory calls, blocks, side-effect policing, and evaluation-once semantics are
outside this implementation. `NullConditionalRewrite.Ignore` remains the default.
Unknown patterns always fail lowering with AM0019; there is no guessed fallback.

Test sources: [generator safety](../tests/AlephMapper.Tests/SemanticSafetyGeneratorTests.cs),
[runtime differential and SQLite](../tests/AlephMapper.IntegrationTests/SwitchParityTests.cs),
[existing semantic regressions](../tests/AlephMapper.IntegrationTests/SemanticRegressionTests.cs).

| Shape | Status | Evidence / limit |
|---|---|---|
| int, decimal, char, enum, string, null constants | Supported on scalar/nullable scalar inputs | `Nullable_constants_results_and_nested_scopes_match_original`, existing string normalization compilation test |
| Overloaded equality null matching | Supported via boxed reference comparison | `Null_constant_patterns_do_not_invoke_overloaded_equality` |
| Relational and logical/parenthesized patterns | Supported on verified scalar inputs | Existing relational SQLite and parenthesized declaration regressions; `Logical`, `Narrowing` differential mappers |
| Type-only / declaration / var / discard | Supported | `New_patterns_generate_compilable_expressions`, property differential suite; final arm must still be `_` |
| Empty, multiple, nested, extended, typed properties | Supported | `Property_patterns_match_original_including_nested_bindings_and_nulls`, `Property_pattern_executes_in_SQLite` |
| Nested member bindings, helper symbols and hidden members | Supported | `Nested`, `Helper`, `Inlined_property_pattern_preserves_base_member_binding` including hidden governing path |
| Fixed array / List<T> | Supported for SZ rank-one arrays and exact List<T> | `List_patterns_generate_compilable_expressions`, `List_patterns_match_original_at_length_boundaries`; null, lengths 0–4, captures |
| Non-capturing list slice | Supported | Same list suite: prefix, suffix, both, empty `[..]`; suffix uses length subtraction |
| Nested list elements | Supported | `NestedElements` in property differential suite |
| Slice capture | Unsupported AM0019 | `Unsupported_patterns_are_diagnosed_without_crashing`; no expression-compatible materialization promised |
| Other custom indexable collections, string list pattern | Deferred, AM0019 | String list negative regression; only arrays/List<T> are verified |
| Record class/struct positional patterns | Supported for source-defined synthesized Deconstruct and positional properties | `Synthesized_record_positional_pattern_generates_compilable_expression`, `Record_patterns_match_original` |
| Nullable record struct property | Supported | `NullablePoint` differential mapper |
| Custom Deconstruct, tuples, metadata-only records | Deferred, AM0019 | Custom deconstruct negative regression; component relationship must be verified from source parameter syntax |
| Nested switches, guards and implicit branch conversions | Supported | `NestedSwitch`, `IsGuard`, `Result` differential mappers and existing cast/coalescing/guard regressions |
| Capture-free `is` | Supported through shared converter | `IsGuard` differential mapper |
| `is` captures needing outer scope | Deferred, AM0019 | `Unrepresentable_expression_shapes_report_AM0019` |
| Missing final unguarded `_`, including final var | Unsupported by design, AM0019 | Existing non-exhaustive test and `Unrepresentable_expression_shapes_report_AM0019` |
| NaN constant, including not NaN | Unsupported by design, AM0019 | Existing NaN negative test |
| Boxed/object/ValueType/interface/Enum scalar comparisons | Deferred, AM0019 | Existing boxed relational test; expanded negative matrix covers nested object, IComparable constant/relational and System.Enum constant |
| Ref-return properties | Unsupported AM0019 | `Ref_return_property_pattern_reports_AM0019`; avoids CS8153 |
| Unknown pattern kind | Unsupported AM0019 | Converter default returns failure; unsupported list/deconstruct siblings exercise rejection without AM0004 |
| Throw expression in expression companion | Unsupported AM0019 | `Unrepresentable_expression_shapes_report_AM0019` |
| Adapt expression / map+expression | Supported | `Property_patterns_work_in_adapted_expression_paths` compiles both outputs |
| Update-only non-exhaustive switch | Ordinary C# retained | Existing `Update_only_adaptation_preserves_non_exhaustive_switch` |
| Repeated input/helper evaluation | Deliberately permitted, no AM0020 | Existing effectful input/argument compilation regressions |

Stable inputs are used for differential checks; the suite compares exception
categories for throwing property getters too. Null and length checks precede
member/index access. Repeated getters can observe mutation differently from C#;
that is the documented conditional-lowering tradeoff, including nested paths.

Conceptual upstream benchmark subset: [ExpressiveSharp pattern reference](https://github.com/EFNext/ExpressiveSharp/blob/main/docs/reference/pattern-matching.md)
and [switch reference](https://github.com/EFNext/ExpressiveSharp/blob/main/docs/reference/switch-expressions.md).
The property, list-prefix, positional-point, numeric-band and guarded-binding
examples were independently exercised by the tests above; upstream generator tests
were not imported or executed. Arbitrary Deconstruct and switch-without-discard
behavior intentionally differ.

## Stage record

All stages retain the original PR branch. Commands used throughout:
`dotnet build AlephMapper.slnx -c Release --no-restore`, followed by
`dotnet test AlephMapper.slnx -c Release --no-build`.

| Stage | Changes and verification | Commit |
|---|---|---|
| S00 | Restore/build baseline; 125 passed, zero failures/skips; SDK 10.0.403 | Recorded with S01 |
| S01 | Structured result + symbol-keyed binding paths; old declaration/collision regressions unchanged; 125 passed | c598813 |
| S02/S03 | Type/var/property/extended path bindings; generator compilation, runtime/null/derived/SQLite checks; 132 passed | afd00d9 |
| S04/S05 | Verified array/List<T>, short-circuit length guards, prefix/suffix slices; negative captured slices/custom deconstruct; 139 passed | 002a615 |
| S06/S07 | Verified positional records, shared is-pattern converter, branch conversions and nullable/nested cases; 145 passed after expanded negatives | ea11a8a |
| S08 review fixes | AND narrowing, helper input conversions before hidden-member access, ref-return rejection, scalar comparison validation; 155 passed | 08b5465 |

Fresh independent Codex review found four actionable correctness gaps; each was
fixed with a targeted regression. Follow-up reviews verified 08b5465 and 9aeddd2. The final review found no remaining concrete P1/P2 issues; the additional lambda-conversion finding was fixed with compilation and runtime regressions.
Existing NU1902/NU1903 dependency advisories remain. Generated nullable cast/access
warnings are visible; compilation checks require zero errors, not hidden warnings.


## Final verification (2026-11-03)

- `dotnet restore AlephMapper.slnx`: succeeded.
- `dotnet build AlephMapper.slnx -c Release --no-restore`: succeeded, zero errors. Clean regeneration reports dependency advisories and generated nullable warnings (41 warnings before the final lambda regression; incremental final build has the same warning categories).
- `dotnet test AlephMapper.slnx -c Release --no-build --no-restore -- --report-trx --results-directory artifacts/test-results`: **157 passed, 0 failed, 0 skipped**.
- `dotnet pack source/AlephMapper.csproj -c Release --no-restore --output artifacts/nuget`: succeeded, `AlephMapper.0.7.4.nupkg`.
- CI workflow `.github/workflows/build.yml` package-consumer fixture recreated in `artifacts/package-smoke` with the same net10.0 project, PackageReference and mapper. `dotnet restore artifacts/package-smoke/PackageSmoke.csproj --source artifacts/nuget --packages artifacts/package-smoke/packages` then `dotnet build artifacts/package-smoke/PackageSmoke.csproj -c Release --no-restore`: succeeded, zero warnings/errors. The isolated package cache ensures this consumes the newly packed local artifact.
- `git diff --check`: passed.

Additional review fix commit: `9aeddd2`, grouping converted lambda operands before helper substitution. No public attributes changed, no AM0020 restored, no expression-factory architecture introduced, and no PR merge/rebase performed. Existing untracked user documents are intentionally excluded from commits under the user's override of the clean-tree gate.

Next work, if expanded later: prove readable slice materialization and custom indexable/Deconstruct equivalents, widen `is` capture scopes, and add provider-specific translation evidence. These remain explicit AM0019 limitations rather than claimed parity.
