using AlephMapper;
using static System.Math;

namespace AlephMapper.IntegrationTests;

public class GeneratedSourceIdentityIntegrationTests
{
    [Test]
    public async Task Static_import_expression_agrees_with_original_mapping()
    {
        var expression = StaticImportMapper.MapExpression().Compile();
        foreach (var input in new[] { -7, 0, 12 })
        {
            await Assert.That(expression(input)).IsEqualTo(StaticImportMapper.Map(input));
        }
    }

    [Test]
    public async Task Same_named_nested_mappers_have_separate_companions()
    {
        var first = FirstContainer.Mapper.MapExpression().Compile();
        var second = SecondContainer.Mapper.MapExpression().Compile();
        await Assert.That(first(4)).IsEqualTo(FirstContainer.Mapper.Map(4));
        await Assert.That(second(4)).IsEqualTo(SecondContainer.Mapper.Map(4));
        await Assert.That(first(4)).IsEqualTo(5);
        await Assert.That(second(4)).IsEqualTo(6);
    }

    [Test]
    public async Task Stable_nested_helper_switch_and_null_paths_match_handwritten_mapping()
    {
        var expression = EquivalenceMapper.MapExpression().Compile();
        foreach (var source in new[]
                 {
                     new EquivalenceSource { Child = null, Score = -2 },
                     new EquivalenceSource { Child = new EquivalenceChild { Name = "Ada" }, Score = 0 },
                     new EquivalenceSource { Child = new EquivalenceChild { Name = "Lin" }, Score = 4 }
                 })
        {
            var ordinary = EquivalenceMapper.Map(source);
            var projected = expression(source);
            await Assert.That(projected.Name).IsEqualTo(ordinary.Name);
            await Assert.That(projected.Rank).IsEqualTo(ordinary.Rank);
            await Assert.That(projected.Doubled).IsEqualTo(ordinary.Doubled);
        }

        Exception? ordinaryFailure = null;
        Exception? projectedFailure = null;
        try { EquivalenceMapper.Map(null!); } catch (Exception error) { ordinaryFailure = error; }
        try { expression(null!); } catch (Exception error) { projectedFailure = error; }
        await Assert.That(ordinaryFailure?.GetType()).IsEqualTo(typeof(NullReferenceException));
        await Assert.That(projectedFailure?.GetType()).IsEqualTo(ordinaryFailure?.GetType());
    }
}

public static partial class StaticImportMapper
{
    [Projectable]
    public static int Map(int value) => Abs(value);
}

public partial class FirstContainer
{
    public static partial class Mapper
    {
        [Projectable]
        public static int Map(int value) => value + 1;
    }
}

public partial class SecondContainer
{
    public static partial class Mapper
    {
        [Projectable]
        public static int Map(int value) => value + 2;
    }
}

public sealed class EquivalenceChild
{
    public string Name { get; set; } = "";
}

public sealed class EquivalenceSource
{
    public EquivalenceChild? Child { get; set; }
    public int Score { get; set; }
}

public sealed class EquivalenceDestination
{
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public int Doubled { get; set; }
}

public static partial class EquivalenceMapper
{
    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static EquivalenceDestination Map(EquivalenceSource source) => new()
    {
        Name = source.Child?.Name ?? "",
        Rank = source.Score switch { < 0 => -1, 0 => 0, _ => 1 },
        Doubled = Double(source.Score + 1)
    };

    private static int Double(int value) => value * 2;
}
