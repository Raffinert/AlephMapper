using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class SemanticRegressionTests
{
    [Test]
    public async Task Type_patterns_keep_inheritance_and_null_semantics()
    {
        var projection = SemanticRegressionMapper.TypeRankExpression().Compile();

        await Assert.That(projection(new SemanticDerived { Score = 7 })).IsEqualTo(7);
        await Assert.That(projection(new SemanticAnimal { Score = 2 })).IsEqualTo(2);
        await Assert.That(projection(null)).IsEqualTo(-1);
        await Assert.That(SemanticRegressionMapper.TypeRank(new SemanticDerived { Score = 7 })).IsEqualTo(7);
        await Assert.That(SemanticRegressionMapper.TypeRankExpression().ToString()).DoesNotContain("GetType");
    }

    [Test]
    public async Task Relational_and_logical_patterns_translate_in_SQLite()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;

        var results = await context.Employees
            .OrderBy(e => e.Id)
            .Select(SemanticRegressionMapper.GradeExpression())
            .ToListAsync();

        await Assert.That(results.Count).IsEqualTo(6);
        await Assert.That(results[0]).IsEqualTo(SemanticRegressionMapper.Grade(new Employee { Id = 1 }));
        await Assert.That(results[3]).IsEqualTo(SemanticRegressionMapper.Grade(new Employee { Id = 4 }));
    }

    [Test]
    public async Task Parameter_substitution_uses_symbols_not_member_names()
    {
        var model = new SemanticInput { Name = "Ada", Count = 3 };
        var generated = SemanticRegressionMapper.NameCollisionExpression().Compile()(model);
        var original = SemanticRegressionMapper.NameCollision(model);

        await Assert.That(generated).IsEqualTo(original);
        await Assert.That(generated).IsEqualTo("Ada/Ada");
    }

    [Test]
    public async Task Conditional_indexer_substitutes_inlined_index_parameter()
    {
        var input = new SemanticIndexSource
        {
            Values = [10, 20, 30, 40],
            Offset = 1
        };

        await Assert.That(SemanticRegressionMapper.IndexWithIgnoreExpression().Compile()(input))
            .IsEqualTo(SemanticRegressionMapper.IndexWithIgnore(input));
        await Assert.That(SemanticRegressionMapper.IndexWithIgnore(input)).IsEqualTo(30);
        await Assert.That(SemanticRegressionMapper.IndexWithRewriteExpression().Compile()(input))
            .IsEqualTo(SemanticRegressionMapper.IndexWithRewrite(input));

        input.Values = null;
        await Assert.That(SemanticRegressionMapper.IndexWithRewriteExpression().Compile()(input))
            .IsEqualTo(-1);
    }

    [Test]
    public async Task Parenthesized_declaration_pattern_uses_original_local_symbol()
    {
        var value = new SemanticPatternAnimal
        {
            Score = 2,
            Other = new SemanticPatternAnimal { Score = 7 }
        };

        var original = SemanticRegressionMapper.ParenthesizedPatternCollision(value);
        var projected = SemanticRegressionMapper.ParenthesizedPatternCollisionExpression().Compile()(value);

        await Assert.That(original).IsEqualTo(7);
        await Assert.That(projected).IsEqualTo(original);

        value.Other.Score = 3;
        await Assert.That(SemanticRegressionMapper.ParenthesizedPatternCollision(value)).IsEqualTo(-1);
        await Assert.That(SemanticRegressionMapper.ParenthesizedPatternCollisionExpression().Compile()(value))
            .IsEqualTo(-1);
    }

    [Test]
    public async Task Pattern_local_name_cannot_capture_substituted_caller_parameter()
    {
        var value = new SemanticPatternAnimal
        {
            Score = 2,
            Other = new SemanticPatternAnimal { Score = 7 }
        };

        var actual = SemanticRegressionMapper.PatternCollisionExpression().Compile()(value);
        await Assert.That(actual).IsEqualTo(SemanticRegressionMapper.PatternCollision(value));
        await Assert.That(actual).IsEqualTo(9);
    }

    [Test]
    public async Task Repeated_method_group_helper_in_LINQ_chain_is_not_circular()
    {
        var source = new SemanticNormalizationSource { Items = [1, 2, 3] };
        var expected = SemanticRegressionMapper.NormalizeTwice(source);
        var generated = SemanticRegressionMapper.NormalizeTwiceExpression().Compile()(source);

        await Assert.That(expected).IsEquivalentTo(new[] { 3, 4, 5 });
        await Assert.That(generated).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Method_group_receiver_uses_substituted_helper_argument()
    {
        var node = new SemanticNode
        {
            Items = [new SemanticItem { Value = 100 }],
            Other = new SemanticNode
            {
                Items = [new SemanticItem { Value = 7 }, new SemanticItem { Value = 8 }]
            }
        };

        var original = SemanticRegressionMapper.MapItems(node);
        var projected = SemanticRegressionMapper.MapItemsExpression().Compile()(node);

        await Assert.That(projected.Values).IsEquivalentTo(original.Values);
        await Assert.That(projected.Values).IsEquivalentTo(new[] { 7, 8 });
        await Assert.That(projected.Values).DoesNotContain(100);
    }

    [Test]
    public async Task Inlined_switch_arm_keeps_cast_coalesce_and_return_conversions()
    {
        var half = SemanticRegressionMapper.CastSwitchArmExpression().Compile();
        await Assert.That(half(3)).IsEqualTo(SemanticRegressionMapper.CastSwitchArm(3));
        await Assert.That(half(3)).IsEqualTo(1d);

        var coalesce = SemanticRegressionMapper.CoalescingSwitchArmExpression().Compile();
        var source = new SemanticCoalescingInput { Flag = true, A = 16777217, B = null };
        await Assert.That(coalesce(source)).IsEqualTo(SemanticRegressionMapper.CoalescingSwitchArm(source));
        await Assert.That(coalesce(source)).IsEqualTo(16777216d);

        var longResult = SemanticRegressionMapper.ConvertedReturnSwitchArmExpression().Compile();
        await Assert.That(longResult(2)).IsEqualTo(SemanticRegressionMapper.ConvertedReturnSwitchArm(2));
        await Assert.That(longResult(2)).IsEqualTo(4294967294L);
    }

    [Test]
    public async Task Null_constant_patterns_do_not_invoke_overloaded_equality()
    {
        var mapped = SemanticRegressionMapper.NotNullWithOverloadedEqualityExpression().Compile();
        await Assert.That(mapped(new SemanticOddEquality())).IsEqualTo(1);
        await Assert.That(mapped(null)).IsEqualTo(0);
    }

    [Test]
    public async Task Nested_rewritten_nullable_helper_receiver_is_not_rejected()
    {
        var projected = SemanticRegressionMapper.NestedNullableNameExpression().Compile();
        var root = new SemanticNullableNode
        {
            Child = new SemanticNullableNode
            {
                Child = new SemanticNullableNode { Name = "Ada" }
            }
        };
        await Assert.That(projected(root)).IsEqualTo("Ada");
        await Assert.That(projected(new SemanticNullableNode())).IsNull();
    }

    [Test]
    public async Task Inlined_helper_in_switch_arm_preserves_multiplication_precedence()
    {
        var projection = SemanticRegressionMapper.SwitchArmHelperPrecedenceExpression().Compile();
        var directProjection = SemanticRegressionMapper.DirectHelperPrecedenceExpression().Compile();

        await Assert.That(SemanticRegressionMapper.SwitchArmHelperPrecedence(1)).IsEqualTo(4);
        await Assert.That(projection(1)).IsEqualTo(4);
        await Assert.That(projection(2)).IsEqualTo(0);
        await Assert.That(SemanticRegressionMapper.DirectHelperPrecedence(1)).IsEqualTo(4);
        await Assert.That(directProjection(1)).IsEqualTo(4);
    }

    [Test]
    public async Task Inlined_switch_guard_preserves_logical_or_precedence()
    {
        var projection = SemanticRegressionMapper.SwitchWithLogicalGuardExpression().Compile();
        var samples = new[]
        {
            new SemanticGuardInput { Count = 2, A = false, B = true }, // OR must not bypass Count == 1
            new SemanticGuardInput { Count = 1, A = false, B = true },
            new SemanticGuardInput { Count = 1, A = true, B = false },
            new SemanticGuardInput { Count = 1, A = false, B = false },
            new SemanticGuardInput { Count = 2, A = true, B = true }
        };
        var expected = new[] { 0, 1, 1, 0, 0 };

        for (var i = 0; i < samples.Length; i++)
        {
            await Assert.That(SemanticRegressionMapper.SwitchWithLogicalGuard(samples[i]))
                .IsEqualTo(expected[i]);
            await Assert.That(projection(samples[i])).IsEqualTo(expected[i]);
        }
    }

    [Test]
    public async Task Inlined_switch_governing_expression_preserves_logical_precedence()
    {
        var expression = SemanticRegressionMapper.SwitchFromLogicalHelperExpression().Compile();
        var samples = new[]
        {
            new SemanticBooleanInput { A = false, B = true },
            new SemanticBooleanInput { A = true, B = false },
            new SemanticBooleanInput { A = true, B = true },
            new SemanticBooleanInput { A = false, B = false }
        };
        var expected = new[] { 1, 1, 0, 1 };

        for (var i = 0; i < samples.Length; i++)
        {
            await Assert.That(SemanticRegressionMapper.SwitchFromLogicalHelper(samples[i]))
                .IsEqualTo(expected[i]);
            await Assert.That(expression(samples[i])).IsEqualTo(expected[i]);
        }
    }

    [Test]
    public async Task Inlining_preserves_parentheses_and_operator_precedence()
    {
        var model = new SemanticInput { Count = 3 };
        var generated = SemanticRegressionMapper.ArithmeticExpression().Compile()(model);

        await Assert.That(generated).IsEqualTo(SemanticRegressionMapper.Arithmetic(model));
        await Assert.That(generated).IsEqualTo(8);
    }
}

public class SemanticAnimal
{
    public int Score { get; set; }
}

public sealed class SemanticDerived : SemanticAnimal { }

public sealed class SemanticInput
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public static partial class SemanticRegressionMapper
{
    [Projectable]
    public static double CastSwitchArm(int value) => value switch
    {
        // Keep the explicit cast: the test checks switch-arm cast preservation.
        // ReSharper disable once RedundantCast
        3 => (double)HalfInt(value),
        _ => 0d
    };

    private static int HalfInt(int value) => value / 2;

    [Projectable]
    public static double CoalescingSwitchArm(SemanticCoalescingInput source) =>
        source.Flag switch
        {
            true => PickCoalesce(source.A, source.B) ?? 0d,
            _ => -1d
        };

    private static float? PickCoalesce(int? a, float? b) => a ?? b;

    [Projectable]
    public static long ConvertedReturnSwitchArm(int value) => value switch
    {
        2 => AsLong(value) * int.MaxValue,
        _ => 0L
    };

    private static long AsLong(int value) => value;

    [Projectable]
    public static int NotNullWithOverloadedEquality(SemanticOddEquality? value) => value switch
    {
        not null => 1,
        _ => 0
    };

    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static string? NestedNullableName(SemanticNullableNode source) =>
        NullableName(source.Child?.Child);

    private static string? NullableName(SemanticNullableNode? node) => node?.Name;

    [Projectable]
    public static int SwitchArmHelperPrecedence(int value) => value switch
    {
        1 => AddOne(value) * 2,
        _ => 0
    };

    [Projectable]
    public static int DirectHelperPrecedence(int value) => AddOne(value) * 2;

    private static int AddOne(int number) => number + 1;

    [Projectable]
    public static int SwitchWithLogicalGuard(SemanticGuardInput source) =>
        source.Count switch
        {
            1 when Allowed(source) => 1,
            _ => 0
        };

    private static bool Allowed(SemanticGuardInput source) => source.A || source.B;

    [Projectable]
    public static int SwitchFromLogicalHelper(SemanticBooleanInput value) =>
        Both(value) switch
        {
            false => 1,
            _ => 0
        };

    private static bool Both(SemanticBooleanInput value) => value.A && value.B;

    [Projectable]
    public static int TypeRank(SemanticAnimal? animal) => animal switch
    {
        SemanticDerived derived when derived.Score > 3 => derived.Score,
        SemanticAnimal other => other.Score,
        _ => -1
    };

    [Projectable]
    public static string Grade(Employee employee) => employee.Id switch
    {
        >= 4 and <= 5 => "HIGH",
        > 0 => "LOW",
        _ => "ZERO"
    };

    [Projectable]
    public static string NameCollision(SemanticInput model) => Join(model, model.Name);

    private static string Join(SemanticInput person, string Name) => person.Name + "/" + Name;

    [Projectable]
    public static int Arithmetic(SemanticInput model) => Twice(model.Count + 1);

    private static int Twice(int number) => number * 2;

    [Projectable]
    public static SemanticArrayDto MapItems(SemanticNode source) => MapItemsHelper(source.Other);

    private static SemanticArrayDto MapItemsHelper(SemanticNode source) => new()
    {
        Values = source.Items.Select(MapItem).ToArray()
    };

    private static int MapItem(SemanticItem item) => item.Value;

    [Projectable]
    public static int[] NormalizeTwice(SemanticNormalizationSource source) =>
        source.Items.Select(Normalize).Select(Normalize).ToArray();

    private static int Normalize(int item) => item + 1;

    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
    public static int? IndexWithIgnore(SemanticIndexSource source) =>
        IndexHelperIgnore(source.Values!, source.Offset + 1);

    [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
    public static int IndexWithRewrite(SemanticIndexSource source) =>
        IndexHelperRewrite(source.Values, source.Offset + 1);

    private static int? IndexHelperIgnore(int[]? values, int index) =>
        values?[index];

    private static int IndexHelperRewrite(int[]? values, int index) =>
        values?[index] ?? -1;

    [Projectable]
    public static int ParenthesizedPatternCollision(SemanticPatternAnimal value) =>
        ParenthesizedPatternHelper(value.Other);

    private static int ParenthesizedPatternHelper(SemanticPatternAnimal animal) =>
        animal switch
        {
            ((SemanticPatternAnimal value)) when value.Score > 5 => value.Score,
            _ => -1
        };

    [Projectable]
    public static int PatternCollision(SemanticPatternAnimal value) =>
        PatternCollisionHelper(value.Other, value);

    private static int PatternCollisionHelper(
        SemanticPatternAnimal animal, SemanticPatternAnimal bonus) =>
        animal switch
        {
            SemanticPatternAnimal value when value.Score > 3 => value.Score + bonus.Score,
            _ => -1
        };
}

public sealed class SemanticNode
{
    public List<SemanticItem> Items { get; set; } = [];
    public SemanticNode Other { get; set; } = null!;
}

public sealed class SemanticItem
{
    public int Value { get; set; }
}

public sealed class SemanticArrayDto
{
    public int[] Values { get; set; } = [];
}

public sealed class SemanticIndexSource
{
    public int[]? Values { get; set; }
    public int Offset { get; set; }
}

public sealed class SemanticPatternAnimal
{
    public int Score { get; set; }
    public SemanticPatternAnimal Other { get; set; } = null!;
}

public sealed class SemanticBooleanInput
{
    public bool A { get; set; }
    public bool B { get; set; }
}

public sealed class SemanticGuardInput
{
    public int Count { get; set; }
    public bool A { get; set; }
    public bool B { get; set; }
}

public sealed class SemanticNormalizationSource
{
    public List<int> Items { get; set; } = [];
}

public sealed class SemanticCoalescingInput
{
    public bool Flag { get; set; }
    public int? A { get; set; }
    public float? B { get; set; }
}

public sealed class SemanticNullableNode
{
    public SemanticNullableNode? Child { get; set; }
    public string? Name { get; set; }
}

public sealed class SemanticOddEquality
{
    public static bool operator ==(SemanticOddEquality? a, SemanticOddEquality? b) => true;
    public static bool operator !=(SemanticOddEquality? a, SemanticOddEquality? b) => false;
    public override bool Equals(object? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => base.GetHashCode();
}
