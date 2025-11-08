using AgileObjects.ReadableExpressions;
using AlephMapper;

namespace AlephMapper.Tests;

public class ValueSource
{
    public int? Maybe { get; set; }
}

public class ValueDest
{
    public int Must { get; set; }
}

public static partial class NullCoalesceValueAssignmentMapper
{
    // In Ignore mode the null-conditional operator is dropped, but the coalesce (??)
    // must be preserved so the assignment to a non-nullable value property still compiles.
    [Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
    public static ValueDest Map(ValueSource s) => new()
    {
        Must = s?.Maybe ?? 42
    };
}

public class IgnoreNullCoalesceValueAssignmentTests
{
    [Test]
    public async Task IgnoreMode_Preserves_Coalesce_For_NonNullable_Value_Assignment()
    {
        var expr = NullCoalesceValueAssignmentMapper.MapExpression();
        var readable = expr.ToReadableString();

        // Should inline to normal member access and preserve the coalesce
        await Assert.That(readable).Contains("?? 42");
        await Assert.That(readable).DoesNotContain("?.");
        await Assert.That(readable).Contains("s.Maybe");

        // And the compiled expression should behave correctly
        var compiled = expr.Compile();
        var withValue = compiled(new ValueSource { Maybe = 5 });
        await Assert.That(withValue.Must).IsEqualTo(5);

        var withoutValue = compiled(new ValueSource { Maybe = null });
        await Assert.That(withoutValue.Must).IsEqualTo(42);
    }
}
