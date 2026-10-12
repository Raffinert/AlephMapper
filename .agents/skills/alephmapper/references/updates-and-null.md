# Updating existing objects and null semantics

## Updatable companion

Annotate an expression-bodied mapping to generate an overload accepting the destination as a final argument.

~~~csharp
using AlephMapper;

public static partial class CustomerMapper
{
    [Updatable]
    public static Customer Map(CustomerUpdateDto source) => new()
    {
        FirstName = source.FirstName,
        LastName = source.LastName
    };
}

var existing = await db.Customers.FindAsync(id);
if (existing is null) return;

CustomerMapper.Map(request, existing); // generated update overload; preserves root instance
await db.SaveChangesAsync();
~~~

Use positional arguments: the current generator calls its destination parameter `dest`. The original one-argument `Map(request)` still creates a mapped value. Review nested reference and null semantics in the generated code; this is **not** EF `ExecuteUpdate`, a bulk SQL update, or an automatic patch engine.

Collections are skipped by default. Opt into `[Updatable(CollectionProperties = CollectionPropertiesPolicy.Update)]` only if collection mutation is genuinely required. Test tracked EF relationships and side effects before enabling it. Updating a non-nullable value-type destination produces diagnostic `AM0001` rather than useful update-in-place semantics.

## Null conditional policy for generated expressions

| `NullConditionalRewrite` | Generated expression behavior |
|---|---|
| `Ignore` (default) | Removes `?.`; `source.Address?.City` becomes member access `source.Address.City`. This may change behavior for null navigation. |
| `Rewrite` | Emits explicit null checks, preserving the intended conditional semantics where supported. |
| `None` | Refuses unsupported `?.` expression generation with `AM0017` rather than emitting invalid expression trees. |

Use `[Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]` at the method or class when null-conditional behavior matters to a provider-facing expression. This setting affects generated expression behavior, not the original handwritten C# method.

For nullable extension mapping, the safe idiom is a stable receiver such as `person.Address?.ToDto()` with `Rewrite`. It is not interchangeable with calling `MapAddress(person.Address)`. A receiver like `GetAddress()?.ToDto()` may be evaluated multiple times by a rewrite; AlephMapper skips unsafe expression generation with `AM0016`.

Do not introduce `?.` indiscriminately. Inspect generated source and test both null and non-null inputs.
