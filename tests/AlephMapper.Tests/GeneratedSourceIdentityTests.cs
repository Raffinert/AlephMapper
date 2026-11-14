using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;

namespace AlephMapper.Tests;

public class GeneratedSourceIdentityTests
{
    [Test]
    public async Task Static_import_keeps_its_binding_in_generated_expression()
    {
        const string source = """
            using AlephMapper;
            using static System.Math;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int value) => Abs(value);
            }
            public static class Consumer
            {
                public static int Run(int value) => Mapper.MapExpression().Compile()(value);
            }
            """;

        var (input, output, result) = await Generate(source);
        var absCall = (await input.SyntaxTrees.Single().GetRootAsync()).DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Single(call => call.Expression.ToString() == "Abs");
        await Assert.That(input.GetSemanticModel(input.SyntaxTrees.Single())
            .GetSymbolInfo(absCall).Symbol?.ToDisplayString()).IsEqualTo("System.Math.Abs(int)");
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsTrue();
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Static_imported_constants_in_conditions_and_patterns_keep_their_binding()
    {
        const string source = """
            using AlephMapper;
            using static Choices;
            public static class Choices
            {
                public const int Positive = 1;
                public const int Negative = -1;
            }
            public static partial class Mapper
            {
                [Projectable]
                public static int Conditional(int value) => value > 0 ? Positive : Negative;
                [Projectable]
                public static int Pattern(int value) => value switch { Positive => 1, _ => 0 };
            }
            public static class Consumer
            {
                public static int Run() => Mapper.ConditionalExpression().Compile()(2) +
                    Mapper.PatternExpression().Compile()(1);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Static_imported_generic_method_keeps_its_binding()
    {
        const string source = """
            using AlephMapper;
            using static System.Array;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int value) => Empty<int>().Length + value;
            }
            public static class Consumer
            {
                public static int Run(int value) => Mapper.MapExpression().Compile()(value);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Nested_mappers_with_the_same_short_name_get_distinct_files()
    {
        const string source = """
            using AlephMapper;
            public partial class First
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 1;
                }
            }
            public partial class Second
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 2;
                }
            }
            public static class Consumer
            {
                public static int Run(int value) =>
                    First.Mapper.MapExpression().Compile()(value) +
                    Second.Mapper.MapExpression().Compile()(value);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.GeneratedSources.Count(s => s.SourceText.ToString().Contains("MapExpression"))).IsEqualTo(2);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Unattributed_type_does_not_rename_existing_nested_mapper_output()
    {
        const string mapper = """
            using AlephMapper;
            public partial class First
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 1;
                }
            }
            """;
        const string unrelated = "public partial class Second { public static partial class Mapper { } }";

        var (_, _, initial) = await Generate(mapper);
        var (_, output, updated) = await Generate(mapper, unrelated);
        var firstName = initial.GeneratedSources.Single(s => s.SourceText.ToString().Contains("MapExpression")).HintName;
        var nextName = updated.GeneratedSources.Single(s => s.SourceText.ToString().Contains("MapExpression")).HintName;
        await Assert.That(nextName).IsEqualTo(firstName);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Another_emitting_mapper_does_not_rename_existing_mapper_output()
    {
        const string first = """
            using AlephMapper;
            public partial class First
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 1;
                }
            }
            """;
        const string second = """
            using AlephMapper;
            public partial class Second
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 2;
                }
            }
            """;

        var (_, _, initial) = await Generate(first);
        var (_, output, updated) = await Generate(first, second);
        var firstName = initial.GeneratedSources.Single(s => s.SourceText.ToString().Contains("MapExpression")).HintName;
        var nextName = updated.GeneratedSources.Single(s => s.SourceText.ToString().Contains("partial class First")).HintName;
        await Assert.That(nextName).IsEqualTo(firstName);
        await Assert.That(nextName.StartsWith("Mapper_GeneratedMappings_", StringComparison.Ordinal)).IsTrue();
        await Assert.That(GeneratedHintName.Matches(nextName, "Mapper")).IsTrue();
        await Assert.That(updated.GeneratedSources.Select(s => s.HintName).Distinct(StringComparer.OrdinalIgnoreCase)).Count().IsEqualTo(updated.GeneratedSources.Length);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Nonemitting_attributed_types_do_not_rename_existing_mapper_output()
    {
        const string mapper = """
            using AlephMapper;
            public partial class First
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 1;
                }
            }
            """;
        const string unrelated = """
            using AlephMapper;
            public partial class Second { [Projectable] public static partial class Mapper { } }
            public partial class Third
            {
                [Projectable] public static class Mapper
                {
                    public static int Map(int value) => value + 2;
                }
            }
            """;

        var (_, _, initial) = await Generate(mapper);
        var (_, output, updated) = await Generate(mapper, unrelated);
        var firstName = initial.GeneratedSources.Single(s => s.SourceText.ToString().Contains("MapExpression")).HintName;
        var nextName = updated.GeneratedSources.Single(s => s.SourceText.ToString().Contains("MapExpression")).HintName;
        await Assert.That(nextName).IsEqualTo(firstName);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Mapper_names_differing_only_by_case_get_distinct_file_paths()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable] public static int Map(int value) => value + 1;
            }
            public static partial class mapper
            {
                [Projectable] public static int Map(int value) => value + 2;
            }
            public static class Consumer
            {
                public static int Run() => Mapper.MapExpression().Compile()(1) +
                    mapper.MapExpression().Compile()(1);
            }
            """;

        var (_, output, result) = await Generate(source);
        var names = result.GeneratedSources.Where(s => s.SourceText.ToString().Contains("MapExpression"))
            .Select(s => s.HintName).ToArray();
        await Assert.That(names).Count().IsEqualTo(2);
        await Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase)).Count().IsEqualTo(2);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Nested_mapper_requires_partial_class_enclosing_types()
    {
        const string source = """
            using AlephMapper;
            public class NonPartial
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 1;
                }
            }
            public partial record RecordOuter
            {
                public static partial class Mapper
                {
                    [Projectable] public static int Map(int value) => value + 2;
                }
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Count(d => d.Id == "AM0021")).IsEqualTo(2);
        await Assert.That(result.Diagnostics.Where(d => d.Id == "AM0021")
            .All(d => d.GetMessage().Contains("every enclosing type must be a partial class"))).IsTrue();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsFalse();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Alias_bound_in_separate_partial_declarations_is_preserved()
    {
        const string first = """
            using AlephMapper;
            using Item = System.Text.StringBuilder;
            public static partial class Mapper
            {
                [Projectable] public static string First(string value) => new Item(value).ToString();
            }
            """;
        const string second = """
            using AlephMapper;
            using Item = System.Version;
            public static partial class Mapper
            {
                [Projectable] public static string Second(int value) => new Item(value, 0).ToString();
            }
            public static class Consumer
            {
                public static string Run() => Mapper.FirstExpression().Compile()("a") +
                    Mapper.SecondExpression().Compile()(1);
            }
            """;

        var (_, output, result) = await Generate(first, second);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Namespace_local_imports_do_not_leak_from_sibling_namespaces()
    {
        const string source = """
            using AlephMapper;
            namespace First
            {
                using Item = System.Text.StringBuilder;
                public static partial class Mapper
                {
                    [Projectable] public static string Map(string value) => new Item(value).ToString();
                }
            }
            namespace Second
            {
                using Item = System.Version;
                public static partial class Mapper
                {
                    [Projectable] public static string Map(int value) => new Item(value, 0).ToString();
                }
            }
            public static class Consumer
            {
                public static string Run() => First.Mapper.MapExpression().Compile()("x") +
                    Second.Mapper.MapExpression().Compile()(1);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Static_imports_in_partial_declarations_do_not_create_ambiguous_calls()
    {
        const string first = """
            using AlephMapper;
            using static System.Math;
            public static partial class Mapper
            {
                [Projectable] public static int Absolute(int value) => Abs(value);
            }
            """;
        const string second = """
            using AlephMapper;
            using static Alternate;
            public static class Alternate { public static int Abs(int value) => value + 10; }
            public static partial class Mapper
            {
                [Projectable] public static int Shifted(int value) => Abs(value);
            }
            public static class Consumer
            {
                public static int Run() => Mapper.AbsoluteExpression().Compile()(-3) +
                    Mapper.ShiftedExpression().Compile()(3);
            }
            """;

        var (_, output, result) = await Generate(first, second);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Imported_extension_method_remains_bound()
    {
        const string source = """
            using AlephMapper;
            using System.Linq;
            public static partial class Mapper
            {
                [Projectable] public static int Map(int[] values) => values.Select(x => x + 1).Sum();
            }
            public static class Consumer
            {
                public static int Run(int[] values) => Mapper.MapExpression().Compile()(values);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Conflicting_generic_type_imports_keep_original_bindings()
    {
        const string definitions = """
            namespace FirstTypes { public sealed class Box<T> { public string Value => "first"; } }
            namespace SecondTypes { public sealed class Box<T> { public string Value => "second"; } }
            """;
        const string first = """
            using AlephMapper;
            using FirstTypes;
            public static partial class Mapper
            {
                [Projectable] public static string First(int value) => new Box<int>().Value;
            }
            """;
        const string second = """
            using AlephMapper;
            using SecondTypes;
            public static partial class Mapper
            {
                [Projectable] public static string Second(int value) => new Box<int>().Value;
            }
            public static class Consumer
            {
                public static string Run() => Mapper.FirstExpression().Compile()(0) +
                    Mapper.SecondExpression().Compile()(0);
            }
            """;

        var (_, output, result) = await Generate(definitions, first, second);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Namespace_spelling_and_generic_arity_do_not_collide()
    {
        const string source = """
            using AlephMapper;
            namespace A.B { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 1; } }
            namespace A_B { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 2; } }
            public partial class Outer<T> { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 3; } }
            public partial class Outer<T, U> { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 4; } }
            public static class Consumer
            {
                public static int Run() => A.B.Mapper.MapExpression().Compile()(1) +
                    A_B.Mapper.MapExpression().Compile()(1) +
                    Outer<int>.Mapper.MapExpression().Compile()(1) +
                    Outer<int, int>.Mapper.MapExpression().Compile()(1);
            }
            """;

        var (_, output, result) = await Generate(source);
        var names = result.GeneratedSources
            .Where(s => s.SourceText.ToString().Contains("MapExpression"))
            .Select(s => s.HintName).ToArray();
        await Assert.That(names).Count().IsEqualTo(4);
        await Assert.That(names.Distinct(StringComparer.Ordinal)).Count().IsEqualTo(4);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var (_, _, repeated) = await Generate(source);
        await Assert.That(repeated.GeneratedSources.Select(s => s.HintName).OrderBy(s => s))
            .IsEquivalentTo(result.GeneratedSources.Select(s => s.HintName).OrderBy(s => s));
    }

    [Test]
    public async Task Cancelled_generation_does_not_become_a_generator_crash()
    {
        const string source = "using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int x) => x + 1; }";
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create("CancelledGenerator",
            [CSharpSyntaxTree.ParseText(source, parseOptions)], references);
        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var wasCancelled = false;
        try
        {
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        await Assert.That(wasCancelled).IsTrue();
    }

    [Test]
    public async Task Supported_nullable_update_adaptation_switch_and_extension_outputs_are_warning_clean()
    {
        const string source = """
            #nullable enable
            using AlephMapper;
            namespace Safe;
            public sealed class Child { public string Name { get; set; } = ""; }
            public sealed class Person { public Child? Child { get; set; } public int Score { get; set; } }
            public sealed class PersonDto { public string Name { get; set; } = ""; public int Rank { get; set; } }
            public sealed class OtherPerson { public Child? Child { get; set; } public int Score { get; set; } }
            public sealed class OtherDto { public string Name { get; set; } = ""; public int Rank { get; set; } }
            public static partial class Mapper
            {
                [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                [Updatable]
                [Adapt(typeof(OtherPerson), typeof(OtherDto), Name = "Adapted",
                    NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static PersonDto Map(Person source) => new()
                {
                    Name = source.Child?.Name ?? "",
                    Rank = source.Score switch { > 0 => 1, _ => 0 }
                };
            }
            public static partial class Extensions
            {
                extension(Person source)
                {
                    [Projectable]
                    public int DoubleScore => source.Score * 2;
                }
            }
            public static class Consumer
            {
                public static PersonDto Run(Person source) =>
                    Mapper.MapExpression().Compile()(source);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Warning &&
            d.Location.SourceTree?.FilePath?.Contains("GeneratedMappings") == true)).IsEmpty();
    }

    [Test]
    public async Task Nullable_switch_null_checks_are_warning_clean()
    {
        const string source = """
            #nullable enable
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int? value) => value switch
                {
                    null => -1,
                    > 0 => 1,
                    _ => 0
                };
            }
            public static class Consumer
            {
                public static int Run(int? value) => Mapper.MapExpression().Compile()(value);
            }
            """;

        var (_, output, result) = await Generate(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
    }

    [Test]
    public async Task Nullable_switch_does_not_emit_annotations_when_project_disables_them()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable] public static int Map(int? value) =>
                    value switch { null => -1, > 0 => 1, _ => 0 };
            }
            public static class Consumer
            {
                public static int Run(int? value) => Mapper.MapExpression().Compile()(value);
            }
            """;

        var (_, output, result) = await GenerateWithNullableOption(false, source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
    }

    private static async Task<(Compilation Input, Compilation Output, GeneratorRunResult Result)> Generate(params string[] sources)
        => await GenerateWithNullableOption(true, sources);

    private static async Task<(Compilation Input, Compilation Output, GeneratorRunResult Result)> GenerateWithNullableOption(
        bool nullableEnabled, params string[] sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var input = CSharpCompilation.Create("GeneratedSourceIdentity",
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, parseOptions, $"Input{index}.cs")), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullableEnabled ? NullableContextOptions.Enable : NullableContextOptions.Disable));
        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        return (input, output, driver.GetRunResult().Results.Single());
    }
}
