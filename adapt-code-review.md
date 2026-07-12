# AlephMapper — `adapt-attribute` Branch Code Review

## Overview

The branch introduces the `[Adapt]` attribute and source generator support for reusing a mapping method as a compile-time template to generate adapted mappings for explicitly specified source/destination type pairs. Three commits were made:

| Commit | Message | Files Changed |
|--------|---------|---------------|
| `43f7a60` | Add adapt attribute | 7 files (+185) |
| `5ac0cf8` | Enhance source generator for adaptation and add attributes | 20+ files (+413 lines to generator, +88 diagnostics) |
| `6ac903b` | docs | 2 documentation files (+338) |

**Total impact:** ~4,142 insertions across 40 files.

---

## Architecture Summary

### New Types (`source/Attributes.cs`)

```csharp
public enum AdaptGeneration { Map = 1, Expression = 2, Both = 3 }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AdaptAttribute : Attribute
{
	public Type SourceType { get; }
	public Type DestinationType { get; }
	public string Name { get; set; }           // Required when Generate includes Expression
	public AdaptGeneration Generate { get; set; } = AdaptGeneration.Map | AdaptGeneration.Expression;
	public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
}
```

### New Model (`source/Models/AdaptationModel.cs`)

| Property | Purpose |
|----------|---------|
| `SourceType` | Explicitly specified source type for the adaptation |
| `DestinationType` | Explicitly specified destination type |
| `GeneratedName` | Base name for generated methods |
| `Generation` | Which companions to generate (Map / Expression / Both) |
| `NullStrategy` | Null-conditional rewrite policy |
| `Attribute` | The raw `AttributeData` for diagnostics and metadata |

### Source Generator Changes (`source/AlephSourceGenerator.cs`)

The generator now:
1. Extracts `[Adapt]` attributes from template methods via `GetAdaptations()`
2. Validates each adaptation pair (type mismatch, open generics) via `ValidateAdaptation()`
3. Rewrites object creation expressions to target the adapted destination type via `AdaptedDestinationRewriter`
4. Generates adapted mapping methods and/or expression companions alongside original mappings

---

## Critical Issues

### 1. `GetAdaptations()` Never Populates Results

**Location:** `source/AlephSourceGenerator.cs` (line ~350)

```csharp
private static IReadOnlyList<AdaptationModel> GetAdaptations(IMethodSymbol methodSymbol)
{
	var result = new List<AdaptationModel>();
	foreach (var attribute in methodSymbol.GetAttributes())
	{
		if (attribute.AttributeClass?.ToDisplayString() != typeof(AdaptAttribute).FullName)
			continue;

		if (attribute.ConstructorArguments.Length < 2 ||
			attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType ||
			attribute.ConstructorArguments[1].Value is not INamedTypeSymbol destinationType)
			continue;

		// BUG: No .Add() call — result is always empty!
	}
	return result;
}
```

**Impact:** The adaptation feature is completely non-functional. `mm.Adaptations` will always be empty, so no adapted methods are ever generated.

**Fix needed:** Add an `.Add()` call after constructing the `AdaptationModel`:
```csharp
var generatedName = attribute.GetNamedArguments()
	.FirstOrDefault(t => t.IsByName("Name"))
	.Value?.ToString() ?? $"{methodSymbol.Name}";

result.Add(new AdaptationModel(sourceType, destinationType, generatedName, generation, nullStrategy, attribute));
```

### 2. `ValidateAdaptation()` Is Incomplete

**Location:** `source/AlephSourceGenerator.cs` (line ~400)

The method validates:
- Type equality with template parameters → reports `InvalidAdaptType` (AM0005)
- Open generic types → reports `AdaptOpenGenericType` (AM0014)

**Missing validations referenced by diagnostics:**

| Diagnostic | Description | Status |
|------------|-------------|--------|
| AM0006 | Source member path cannot be resolved | ❌ Not implemented |
| AM0007 | Destination member missing or not writable | ❌ Not implemented |
| AM0008 | Incompatible type for adapted expression/assignment | ❌ Not implemented |
| AM0010 | Unsupported syntax in template | ❌ Not implemented |

The helper methods `CanResolveReadablePath()`, `HasWritableInstanceMember()`, and `CollectTopLevelDestinationAssignments()` exist in the diff but their integration into validation is incomplete. The `ValidateAdaptation()` method should verify that all member paths referenced in the template body are resolvable on the adapted source type, and that destination assignments target writable members on the adapted destination type.

### 3. No Integration Tests for Adapt Feature

Test source files exist:
- `tests/AlephMapper.Tests/Files/AdaptBoth/Sources/Source.cs` — simple object creation
- `tests/AlephMapper.Tests/Files/AdaptNested/Sources/Source.cs` — ternary expression with conditional logic

Expected output files exist (`AlephMapper.Attributes.g.cs`, `Tests_PersonMapper_GeneratedMappings.g.cs`) but there is **no test runner class** that compiles these sources against the generator and verifies the generated output matches expectations.

---

## Structural Concerns

### 4. Expression Body Methods Not Handled

The template method in `AdaptExampleMapper.cs` uses block body syntax:
```csharp
public static ApplicantBriefDto MapApplicantBrief(...) => new() { ... };
```

However, if a template uses expression body (`=>`) instead of block body (`{}`), the generator's `ExtractBodyExpression()` may not extract it correctly for adaptation. The current implementation assumes block bodies via `InitializerExpressionSyntax`. Expression body templates should be supported or explicitly documented as unsupported.

### 5. Source Generator Monolith

The ~300-line addition to `AlephSourceGenerator.cs` mixes:
- Adaptation extraction (`GetAdaptations()`)
- Validation logic (`ValidateAdaptation()`)
- Body rewriting (`RewriteAdaptedBody()` + `AdaptedDestinationRewriter`)
- Member path resolution helpers

Consider extracting adaptation-specific logic into a dedicated class such as `AdaptationGenerator` or `AdaptValidationService` for better separation of concerns and testability.

### 6. Diagnostics Could Be Better Organized

Ten new diagnostics (AM0005–AM0014) were added:
- Validation errors: AM0005, AM0006, AM0007, AM0008, AM0009, AM0011, AM0012, AM0014, AM0015
- Warnings: AM0010, AM0013

Consider grouping them into a dedicated `AdaptationDiagnosticDescriptors` partial class or file for clarity.

---

## Positive Observations

### 7. Robust Type Rewriting via Semantic Model

The `AdaptedDestinationRewriter` uses the semantic model to accurately identify object creation expressions matching the original destination type, falling back to string comparison when rebinding fails:
```csharp
private bool IsOriginalDestinationCreation(ExpressionSyntax node)
{
	try {
		var typeInfo = semanticModel.GetTypeInfo(node);
		// ... compare via SymbolEqualityComparer.Default
	} catch (ArgumentException) { /* detached nodes */ }

	// Fallback to string comparison
	if (node is ObjectCreationExpressionSyntax oc) {
		return typeText == originalDestinationType.Name ||
			   typeText == originalDestinationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
	}
}
```

This handles Roslyn's detached node scenario gracefully.

### 8. Duplicate Pair Detection

The generator now tracks adaptation pairs by parameterized signature and reports `AdaptDuplicatePair` (AM0012) when the same source/destination combination is specified multiple times:
```csharp
var adaptPairSignature = BuildMethodSignature("", [adaptSourceFqn, adaptDestFqn]);
if (!adaptationPairs.Add(adaptPairSignature)) { /* report duplicate */ }
```

### 9. Method Signature Conflict Detection

Existing method signatures are collected before generation to prevent conflicts:
```csharp
var existingMethodSignatures = new HashSet<string>(
	mapperType.GetMembers().OfType<IMethodSymbol>()
		.Where(m => m.MethodKind == MethodKind.Ordinary)
		.Select(m => BuildMethodSignature(...)));
```

### 10. Comprehensive Sample App Demonstration

The `AdaptExampleMapper` and `Program.cs` changes provide a clear, runnable example showing:
- Template method with helper functions (`FormatDisplayName`, `FormatContact`, etc.)
- Adaptation from `ApplicantProfile → ApplicantBriefDto` (original)
- Adaptation from `ContractorRecord → ContractorBriefDto` (adapted template reuse)
- Both regular mapping and expression companion generation

---

## Recommendations

| Priority | Action |
|----------|--------|
| **Critical** | Fix `GetAdaptations()` to populate the result list with `.Add()` calls |
| **Critical** | Complete `ValidateAdaptation()` — integrate member path resolution, writable destination checks, and type compatibility validation |
| **High** | Add integration tests that compile AdaptBoth/AdaptNested sources and verify generated output |
| **Medium** | Extract adaptation logic from the monolithic generator into a dedicated class |
| **Low** | Document expression body method support (or explicitly mark as unsupported) |

---

## Summary

The `adapt-attribute` branch introduces an interesting feature for reusing mapping templates across different type pairs. The architecture is sound — the attribute design, model structure, and semantic-model-based rewriting are well thought out. However, two critical bugs (`GetAdaptations()` returning empty results and incomplete validation) render the feature non-functional in its current state. Addressing these should be the highest priority before merging.
