# Extension member compatibility matrix

Scope: readable source-generated projection/inlining for C#14 instance extension
methods and getter-only expression properties. Factories are static methods in the
outer partial class. This is not full ExpressiveSharp parity.

Evidence: [generator tests](../tests/AlephMapper.Tests/ExtensionMemberTests.cs),
[runtime and SQLite tests](../tests/AlephMapper.IntegrationTests/ExtensionMembersParityTests.cs).

| Case / source example | Expected API | Compile | Runtime | SQLite | Status / evidence or reason |
|---|---|---|---|---|---|
| `extension(Order o) { decimal AmountWithTax => ...; }` | `AmountWithTaxExpression()` | Pass | Pass | Pass | CSharp14_properties_methods_and_classic_extensions_compose; CSharp14_projection_executes_in_SQLite |
| `Label(int min)` | `LabelExpression(int min)` | Pass | Pass | Pass | Same, below/equal/above threshold |
| Primitive `int.Squared` | `SquaredExpression()` | Pass | Pass | Not asserted | CSharp14_extension_factories_compile_and_match_source; runtime composition |
| Interface `IEntity.InterfaceLabel` | `InterfaceLabelExpression()` | Pass | Pass | Not asserted | Runtime composition |
| Struct `Point.Sum` | `SumExpression()` | Pass | Pass | Not asserted | Extension_members_preserve_nested_receivers_null_policies_and_method_groups |
| Nullable `int?.OrZero` | `OrZeroExpression()` | Pass | Pass | Not asserted | Same, null/non-null |
| Named/reordered/mixed arguments | Symbol/operation binding | Pass | Pass | Not asserted | Named mapper, including positional after named |
| Overloads with different formal types | `TimesExpression(int)` and `(long)` | Pass | Pass | Not asserted | Composition runtime |
| Same factory signature across receiver blocks | Skip conflicting factories | Pass | N/A | N/A | AM0021; Extension_factory_signature_conflicts_are_diagnosed |
| Separate namespaces / same property name | Separate property symbols | Pass | Not separately run | N/A | Extension_members_resolve_by_symbol_across_namespaces; no residual .Value |
| Property/method and classic/CSharp14 composition | Transitive expansion | Pass | Pass | Pass subset | Composed, Classic, ToDto |
| Nested receivers and composite return precedence | Correct path and grouping | Pass | Pass | Not asserted | Nested mapper, scalar fixtures |
| Switch bodies and guards | Existing switch lowering | Pass | Pass | Pass | Label/ToDto plus full PR56 regression suite |
| `Select(Map)` with extension properties in helper | Existing method-group expansion | Pass | Pass | Not asserted | Group mapper; classic generated mapper snapshots unchanged |
| Bound extension method group | Skip companion; use explicit lambda | Pass | N/A | N/A | AM0021; Bound_extension_method_groups_report_diagnostic |
| Ignore / Rewrite null strategy | Existing behavior plus receiver conversion | Pass | Pass | Not asserted | Nested receivers and Base/Derived hidden member regression |
| None null strategy | Skip expression | Pass | N/A | N/A | AM0017; NullConditional_None_keeps_its_diagnostic_for_extension_members |
| Lambda name collision | Symbol-based alpha-renaming | Pass | Pass | Not asserted | Capture returns14, not5 |
| Static dependencies, nonfinite constants, nameof | Bound symbols / original constant value | Pass | Pass | Not asserted | StaticDependency, Nonfinite, Name and Classic_lambda_renaming_preserves_nameof_and_generic_static_binding runtime regressions |
| Inaccessible qualified/unqualified dependency | Skip affected companion | Pass | N/A | N/A | AM0021; Inaccessible_extension_dependencies_are_diagnosed |
| Escaped receiver/factory argument | Escaped identifiers | Pass | Not separately run | N/A | Escaped_receiver_and_factory_argument_names_compile |
| Partial files, class/member attributes, repeat run | One deterministic file | Pass | N/A | N/A | Extension_members_in_partial_files_generate_once_and_deterministically |
| Method/property cycles | Terminate safely | Pass | N/A | N/A | AM0002; Extension_member_cycles_terminate_with_diagnostic |
| Missing partial outer class | No invalid partial source | Pass | N/A | N/A | AM0021; Missing_partial_outer_class_has_actionable_diagnostic |
| Direct Updatable / Adapt extension request | Projection only, diagnostic for unsupported request | Pass | N/A | N/A | AM0021; direct Updatable regression, ordinary Update/Adapt suite retained |
| Generic blocks/methods | No assumed generic substitution | Pass method fixture | N/A | N/A | AM0021 |
| Optional/params/ref args, ref-like receiver, ref returns | Explicit unsupported reason | Tested optional | N/A | N/A | AM0021; remaining shapes symbol-validated, no exhaustive test claim |
| Block/accessor bodies, static extension members | Skip affected companion | Pass | N/A | N/A | AM0021; unsupported-member matrix |
| Query syntax / block or async lambda | Use LINQ method syntax / expression lambda | Pass query/block fixtures | N/A | N/A | AM0021; prevents range-variable capture or invalid expression trees |
| Cross-assembly source body expansion | Not attempted | N/A | N/A | N/A | Source body required; unexpandable dependency AM0021 |

Matrix reports test evidence instead of a marketing parity percentage. Receiver
and argument evaluation may be repeated, skipped or reordered by the existing
conditional/substitution architecture; no AM0020 or purity gate was introduced.
Property catalog keys are IPropertySymbol identities; getter methods are stable
recursion tokens alongside the existing method catalog, not unrelated methods.

Compiler minimum: Roslyn5.0+ (independent API probe). Verified installed host:
SDK10.0.403/Roslyn5.9.0. Fresh packaged CSharp14/net10 and classic CSharp12/net8
consumers execute successfully without analyzer-load failures. IDE builds must
meet the same host requirement; IDE versions were not separately tested.
Older Roslyn4.x hosts are incompatible. Generator target stays netstandard2.0.

Conceptual inventory: [ExpressiveSharp guide](https://github.com/EFNext/ExpressiveSharp/blob/main/docs/guide/extension-members.md)
and [reference tests](https://github.com/EFNext/ExpressiveSharp/blob/main/tests/ExpressiveSharp.Generator.Tests/ExpressiveGenerator/ExtensionMemberTests.cs).
Upstream test code was not copied or executed. Block-body lowering, interceptors,
ExpressiveFor and cross-assembly source reuse remain outside scope.
