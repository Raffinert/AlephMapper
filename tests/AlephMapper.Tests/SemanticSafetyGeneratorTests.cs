using AlephMapper.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace AlephMapper.Tests;

public class SemanticSafetyGeneratorTests
{
    [Test]
    public async Task Non_exhaustive_switch_reports_AM0019_instead_of_emitting_wrong_expression()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static string Map(int value) => value switch
                {
                    1 => "One",
                    2 => "Two"
                };
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Repeated_switch_governing_is_permitted_for_expressive_sharp_parity()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int value) => Next() switch
                {
                    > 0 and < 10 => 1,
                    _ => 0
                };

                private static int Next() => System.Random.Shared.Next();
            }
            """;

        // Deliberate ExpressiveSharp parity: lowering may repeat the evaluation.
        // AM0019 remains reserved for unsupported or non-exhaustive patterns.
        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task String_normalization_switch_generates_expression()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(string value) => value.ToLower() switch
                {
                    "mobile" => 1,
                    "home" => 2,
                    "work" => 3,
                    _ => 0
                };
            }
            """;

        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
    }

    [Test]
    public async Task Single_use_effectful_switch_governing_remains_supported()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int value) => Next() switch
                {
                    > 0 => 1,
                    _ => 0
                };

                private static int Next() => System.Random.Shared.Next();
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Repeated_side_effectful_argument_reports_AM0020()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int number) => Twice(System.Random.Shared.Next());

                private static int Twice(int value) => value + value;
            }
            """;

        // Random.Next() has observable state; expanding Twice(value) must not
        // evaluate that invocation twice.
        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0020")).IsTrue();
    }


    [Test]
    public async Task Boxed_object_relational_pattern_reports_AM0019()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(object value) => value switch
                {
                    > 0 => 1,
                    _ => 0
                };
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
    }

    [Test]
    public async Task NaN_constant_pattern_reports_AM0019_instead_of_wrong_not_pattern()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(double value) => value switch
                {
                    not double.NaN => 1,
                    _ => 0
                };
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
    }

    [Test]
    public async Task Mutually_exclusive_parameter_reads_do_not_trigger_AM0020()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(bool flag) => Choose(flag, Next());
                private static int Choose(bool flag, int value) => flag ? value : value;
                private static int Next() { return System.Random.Shared.Next(); }
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0020")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Reordered_effectful_arguments_report_AM0020()
    {
        const string source = """
            using AlephMapper;
            public static partial class Mapper
            {
                [Projectable]
                public static int Map(int value) => value switch
                {
                    1 => Subtract(Next(), Next()),
                    _ => 0
                };
                private static int Subtract(int left, int right) => right - left;
                private static int Next() { return System.Random.Shared.Next(); }
            }
            """;

        var result = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0020")).IsTrue();
    }

    [Test]
    public async Task Update_only_adaptation_preserves_non_exhaustive_switch()
    {
        const string source = """
            using AlephMapper;

            public sealed class PersonSource { public int Code { get; set; } }
            public sealed class EmployeeSource { public int Code { get; set; } }
            public sealed class PersonDest { public string Value { get; set; } = ""; }
            public sealed class EmployeeDest { public string Value { get; set; } = ""; }

            public static partial class Mapper
            {
                [Adapt(
                    typeof(EmployeeSource),
                    typeof(EmployeeDest),
                    Name = "MapEmployee",
                    Generate = AdaptGeneration.Update)]
                public static PersonDest Map(PersonSource source) => new()
                {
                    Value = source.Code switch
                    {
                        1 => "one"
                    }
                };
            }
            """;

        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapEmployee(", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapEmployeeExpression(", StringComparison.Ordinal))).IsFalse();
        await Assert.That(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    private static async Task<(GeneratorRunResult Result, Compilation Output)> GenerateAndCompile(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "UpdateOnlySafety",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.RunGenerators(compilation).GetRunResult().Results.Single(), output);
    }

    [Test]
    public async Task New_diagnostic_descriptors_are_registered()
    {
        await Assert.That(DiagnosticDescriptors.GetById("AM0019")).IsEqualTo(DiagnosticDescriptors.UnsupportedSwitchPattern);
        await Assert.That(DiagnosticDescriptors.GetById("AM0020")).IsEqualTo(DiagnosticDescriptors.UnsafeRepeatedArgument);
    }

    private static async Task<GeneratorRunResult> Generate(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "SemanticSafety",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }
}
