# Diagnostic catalog

These are the diagnostic IDs declared by the current generator in [`DiagnosticDescriptors.cs`](../source/Diagnostics/DiagnosticDescriptors.cs). Severity is the default; a consuming project can change it. Locations normally point to the marked mapping/member or the offending syntax. Some validation failures point to the `[Adapt]` declaration; [AM0004](#am0004) can have no source location. A warning suppressed by compiler configuration still leaves any skipped companion absent. Correct the cause before calling a missing generated member.

## AM0001

**Updatable method with value type return type · Warning.** `[Updatable]` on a mapping returning a non-nullable value type cannot update the caller's value by reference. For example, `[Updatable] static int Map(Source s) => s.Number;` has no update companion. Use the handwritten map as a value-returning operation, or return a reference-type destination. The ordinary method remains.

## AM0002

**Projectable method generation skipped due to circular references · Warning.** A projection/helper expansion cycle, such as `A(s) => B(s)` and `B(s) => A(s)`, cannot be inlined. Break the cycle or write a finite expression body. The expression companion is omitted; the handwritten method still has its ordinary C# behavior.

## AM0003

**Updatable method generation skipped due to circular references · Warning.** An update mapping's helper expansion cycles. Replace `Map(s) => Helper(s)` where `Helper` calls `Map` with a finite object initializer such as `Map(s) => new() { Name = s.Name }`, then rebuild. The update overload is omitted.

## AM0004

**Generator crash · Error.** An unexpected generator exception escaped analysis or emission. Reduce the mapper to a minimal reproducer and [open a bug report](https://github.com/Raffinert/AlephMapper/issues/new?labels=bug&title=Generator%20crash:%20AM0004) with the full diagnostic, SDK/compiler version, and source. There is no reliable generated companion from the failed callback. This is an implementation defect, not an invitation to suppress the error.

## AM0005

**Invalid adapted source or destination type · Error.** An `[Adapt]` type argument is invalid for structural substitution. Use concrete, valid source and destination types, for example `[Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]`. The requested adaptation is omitted.

## AM0006

**Required adapted source member is missing · Error.** The template reads a source path that the adapted source lacks, such as `person.Address.City` when `Employee` has no `Address`. Add a compatible member or choose an adapted source with that path. The requested adaptation is omitted.

## AM0007

**Required adapted destination member is missing or not writable · Error.** The template assigns a destination member absent or read-only on the adapted destination. Add a writable corresponding member, for example `public string Name { get; set; } = "";`, or adapt to a compatible destination. The requested adaptation is omitted.

## AM0008

**Adapted expression or assignment has an incompatible type · Error.** A substituted source expression cannot be assigned to the destination member, for example a `string` source member for an `int` destination member. Align types or use a separate explicit mapping. The requested adaptation is omitted.

## AM0009

**Generated adapted method name or signature conflicts · Error.** The chosen `Name` produces a member already present or generated. Give the adaptation a distinct name, for example `Name = "MapEmployee"`. The conflicting adaptation members are omitted.

## AM0010

**Template contains unsupported adaptation syntax · Warning.** The template uses syntax the structural adapter cannot safely rebind. For a destination change, use a recognized construction such as `Map(Source s) => new Destination { Name = s.Name };`, or write the adapted mapping explicitly. The requested adaptation is omitted.

## AM0011

**Expression generation requested without a generated name · Error.** `[Adapt(typeof(Employee), typeof(EmployeeDto))]` defaults to `Map | Expression`, which requires `Name`. Add `Name = "MapEmployee"`, or request only `Generate = AdaptGeneration.Map`. The requested adaptation is omitted.

## AM0012

**Duplicate adaptation for the same explicit type pair · Error.** Two `[Adapt]` declarations on one template specify the same source/destination pair. Keep one `[Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]` declaration per pair; change its `Generate` flags if more companions are needed. The duplicate adaptation is omitted.

## AM0013

**Circular helper reference prevents adaptation · Warning.** The adapted template expands through a recursive helper chain. Break `A(s) => B(s)` / `B(s) => A(s)` or use a direct, finite expression such as `s.Name`. The affected adaptation is omitted.

## AM0014

**Adapted source or destination type is open generic · Error.** An argument such as `typeof(Box<>)` is not a concrete pair. Supply closed types such as `typeof(Box<Person>)`, where compatible. The adaptation is omitted.

## AM0015

**Generated adapted method fails Roslyn rebinding · Error.** After substitution, the proposed member fails compilation validation, for example when the adapted destination lacks the constructor used by `new Destination(value)`. For a constructor mismatch, the diagnostic points at the original `new ...` expression and names the adapted destination and rejected construction. Add a compatible constructor such as `EmployeeDto(string value)`, choose a compatible pair, or write an explicit mapping. The adaptation is omitted; report a minimal reproducer if the pair should be supported.

## AM0016

**Null-conditional receiver cannot be safely rewritten · Warning.** With `NullConditionalRewrite.Rewrite`, an unstable receiver such as `GetAddress()?.City` could be evaluated twice. Store it in a stable member path in the source model or use a mapping shape with safe evaluation. The affected expression/adapted companion is omitted.

## AM0017

**Null-conditional access is unsupported in expression trees · Warning.** `NullConditionalRewrite.None` preserves `?.` in a requested expression tree. Choose `NullConditionalRewrite.Rewrite` for a stable receiver, or deliberately choose `Ignore` only when dereferencing is safe. The affected expression companion is omitted.

## AM0018

**Adaptation of generic mapping methods is unsupported · Warning.** `[Adapt]` on `Map<T>(T source)` is not structurally adapted. Use the generic `[Projectable]` or `[Updatable]` companion directly, or write a closed non-generic template. Adapted companions are omitted; other requested companions may still be generated.

## AM0019

**Switch expression cannot be lowered without changing semantics · Warning.** An unsupported or non-exhaustive switch cannot safely become an expression tree. Add a supported exhaustive arm, such as `_ => fallback`, and consult the [switch matrix](switch-parity-matrix.md). The affected projection or adapted map/expression is omitted. An update-only adaptation can retain an ordinary C# switch.

## AM0020

**Extension member cannot be projected · Warning.** The diagnostic message gives the specific reason, such as an unsupported extension shape, inaccessible dependency, or conflicting generated factory. Use a supported instance expression-bodied extension method or getter-only expression property, for example `extension(Person p) { [Projectable] public string Label => p.Name; }`, in a partial outer class; see the [extension matrix](extension-members-compatibility-matrix.md). The affected extension companion is omitted.

## AM0021

**Mapper declaration cannot contain generated companions · Warning.** The mapper declaration is ineligible, typically because the class is not `static partial`. Change `public static class Mapper` to `public static partial class Mapper`. Its companions are omitted.

## AM0022

**Generated companion conflicts with another member · Warning.** A requested classic projection/update signature already exists, such as a handwritten `MapExpression()` beside `[Projectable] Map(...)`. Rename the existing method or mapping so each signature is unique. The conflicting companion is omitted.

The source of truth for diagnostic IDs is the descriptor declaration plus [analyzer release tracking](../source/AnalyzerReleases.Unshipped.md). For API shapes and troubleshooting, see the [generated API reference](generated-api-reference.md) and [advanced usage](advanced-usage.md#troubleshooting).
