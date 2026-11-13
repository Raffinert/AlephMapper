# Extension member compatibility matrix

Scope: readable source-generated projection/inlining for C# 14 instance extension
methods and getter-only expression properties, plus update/adaptation companions
for instance methods. Generated companions are static members of the outer partial
class. Extension properties remain projection-only. This is not full ExpressiveSharp parity.

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
| Same factory signature across receiver blocks | Skip conflicting factories | Pass | N/A | N/A | AM0020; Extension_factory_signature_conflicts_are_diagnosed |
| Separate namespaces / same property name | Separate property symbols | Pass | Not separately run | N/A | Extension_members_resolve_by_symbol_across_namespaces; no residual .Value |
| Property/method and classic/CSharp14 composition | Transitive expansion | Pass | Pass | Pass subset | Composed, Classic, ToDto |
| Nested receivers and composite return precedence | Correct path and grouping | Pass | Pass | Not asserted | Nested mapper, scalar fixtures |
| Switch bodies and guards | Existing switch lowering | Pass | Pass | Pass | Label/ToDto plus full PR56 regression suite |
| `Select(Map)` with extension properties in helper | Existing method-group expansion | Pass | Pass | Not asserted | Group mapper; classic generated mapper snapshots unchanged |
| Bound extension method group | Skip companion; use explicit lambda | Pass | N/A | N/A | AM0020; Bound_extension_method_groups_report_diagnostic |
| Ignore / Rewrite null strategy | Existing behavior plus receiver conversion | Pass | Pass | Not asserted | Nested receivers and Base/Derived hidden member regression |
| None null strategy | Skip expression | Pass | N/A | N/A | AM0017; NullConditional_None_keeps_its_diagnostic_for_extension_members |
| Lambda name collision / inferred anonymous member name | Symbol-based alpha-renaming | Pass | Pass | Not asserted | Capture returns14, not5; Renamed_lambda_preserves_anonymous_members_and_root_nameof |
| Static dependencies, nonfinite constants, nameof | Bound symbols / original constant value | Pass | Pass | Not asserted | StaticDependency, Nonfinite, Name and Classic_lambda_renaming_preserves_nameof_and_generic_static_binding runtime regressions |
| Inaccessible qualified/unqualified dependency | Skip affected companion | Pass | N/A | N/A | AM0020; Inaccessible_extension_dependencies_are_diagnosed |
| Escaped receiver/factory argument | Escaped identifiers | Pass | Not separately run | N/A | Escaped_receiver_and_factory_argument_names_compile |
| Partial files, class/member attributes, repeat run | One deterministic file | Pass | N/A | N/A | Extension_members_in_partial_files_generate_once_and_deterministically |
| Method/property cycles | Terminate safely | Pass | N/A | N/A | AM0002; Extension_member_cycles_terminate_with_diagnostic |
| Missing partial outer class | No invalid partial source | Pass | N/A | N/A | AM0020; Missing_partial_outer_class_has_actionable_diagnostic |
| `[Updatable]` on instance extension method returning reference DTO | `Map(receiver, ..., dest)` | Covered | Covered | N/A | CSharp14_extension_method_generates_projection_update_and_adapted_companions; CSharp14_extension_update_and_adapt_companions_preserve_destinations |
| `[Adapt]` on instance extension method | `MapOther(receiver, ...)`, `MapOtherExpression(...)`, optional `MapOther(receiver, ..., dest)` | Covered | Covered | N/A | Same tests; type and member rebinding follows classic Adapt validation |
| Projectable extension method used with SQLite | `MapExpression(...)` | Covered | Covered | Covered | CSharp14_projection_executes_in_SQLite |
| `[Updatable]` on scalar/value-type return | No update companion; AM0001 | Covered | N/A | N/A | Unsupported_scalar_extension_update_reports_value_type_diagnostic |
| Computed extension property | Projection-only; update/adaptation not accepted | As designed | N/A | N/A | `[Updatable]` and `[Adapt]` attribute targets exclude properties |
| Generic blocks/methods | No assumed generic substitution | Pass method fixture | N/A | N/A | AM0020 |
| Optional/params/ref args, ref-like receiver, ref returns | Explicit unsupported reason | Tested optional | N/A | N/A | AM0020; remaining shapes symbol-validated, no exhaustive test claim |
| Block/accessor bodies, static extension members | Skip affected companion | Pass | N/A | N/A | AM0020; unsupported-member matrix |
| Query syntax / block or async lambda | Use LINQ method syntax / expression lambda | Pass query/block fixtures | N/A | N/A | AM0020; prevents range-variable capture or invalid expression trees |
| Cross-assembly source body expansion | Not attempted | N/A | N/A | N/A | Source body required; unexpandable dependency AM0020 |

Matrix reports test evidence instead of a marketing parity percentage. Receiver
and argument evaluation may be repeated, skipped or reordered by the existing
conditional/substitution architecture; no purity gate was introduced. `AM0020`
reports unsupported extension members.
Property catalog keys are IPropertySymbol identities; getter methods are stable
recursion tokens alongside the existing method catalog, not unrelated methods.

Compiler/package support is now versioned: classic mappings use the Roslyn4.8
asset on4.x hosts; CSharp14 extension features use the Roslyn5.0 asset on5.x hosts.
Both assets remain netstandard2.0. Real compiler processes4.8.0,4.14.0,5.0.0 and the
installed SDK10.0.403/Roslyn5.9 pass package selection and runtime checks.
A net8 target under SDK10 alone did not establish4.x host compatibility; the old
single5.0 package was reproduced failing with CS9057 on compiler4.14.
See [multiversion evidence](roslyn-multiversion-plan.md). Hosts below4.8 remain
unsupported. Individual IDE versions were not separately tested.

Conceptual inventory: [ExpressiveSharp guide](https://github.com/EFNext/ExpressiveSharp/blob/main/docs/guide/extension-members.md)
and [reference tests](https://github.com/EFNext/ExpressiveSharp/blob/main/tests/ExpressiveSharp.Generator.Tests/ExpressiveGenerator/ExtensionMemberTests.cs).
Upstream test code was not copied or executed. Block-body lowering, interceptors,
ExpressiveFor and cross-assembly source reuse remain outside scope.

CSharp14 rejects nameof(extensionMember) in original source (CS9316); this is a
compiler restriction, not a supported generator input. The executable
CSharp14_compiler_rejects_nameof_extension_member_in_original_source test records
it. Ordinary root nameof expressions preserve their original constant values.
