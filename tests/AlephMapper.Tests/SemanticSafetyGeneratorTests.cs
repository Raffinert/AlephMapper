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
        var diagnostic = result.Diagnostics.Single(d => d.Id == "AM0019");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("Companion generation");
        await Assert.That(diagnostic.Descriptor.HelpLinkUri).Contains("docs/diagnostics.md#am0019");
        await Assert.That(source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .Contains("value switch");
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
    public async Task Repeated_side_effectful_helper_argument_is_inlined()
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

        // Intentionally permits repeated evaluation of the inlined argument,
        // matching the substitution behavior used by ExpressiveSharp.
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
        await Assert.That(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
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
    public async Task Mutually_exclusive_parameter_reads_still_inline()
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
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Reordered_effectful_helper_arguments_are_inlined()
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

        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsTrue();
        await Assert.That(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
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

    [Test]
    public async Task Updatable_nameof_type_preserves_original_constant()
    {
        const string source = """
            using System;
            using AlephMapper;

            public sealed class Source { }
            public sealed class Destination { public string Name { get; set; } = ""; }

            public static partial class Mapper
            {
                [Updatable]
                public static Destination Map(Source source) => new() { Name = nameof(Int32) };
            }
            """;

        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var generated = result.GeneratedSources.Single().SourceText.ToString();
        await Assert.That(generated).Contains("dest.Name = \"Int32\"");
    }

    [Test]
    [Arguments("value switch { var x when x == null => 1, _ => 0 }")]
    [Arguments("value switch { Node => 1, _ => 0 }")]
    [Arguments("value switch { { Child: Node c } when c.Score > 0 => c.Score, _ => 0 }")]
    [Arguments("value switch { { Child.Score: > 0 } => 1, _ => 0 }")]
    [Arguments("value switch { { } => 1, _ => 0 }")]
    public async Task New_patterns_generate_compilable_expressions(string expression)
    {
        var source = "using AlephMapper; public class Node { public Node Child {get;set;} public int Score {get;set;} } " +
            "public static partial class Mapper { [Projectable] public static int Map(Node value) => " + expression + "; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsTrue();
    }

    [Test]
    [Arguments("[]")]
    [Arguments("[var x] when x > 0")]
    [Arguments("[1, .., 3]")]
    [Arguments("[.., > 0]")]
    public async Task List_patterns_generate_compilable_expressions(string pattern)
    {
        var source = "using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int[] value) => value switch { " + pattern + " => 1, _ => 0 }; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsTrue();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    [Arguments("int[]", "[.. var rest]")]
    [Arguments("Node", "(var x, var y)")]
    [Arguments("Node", "{ Boxed: > 0 }")]
    [Arguments("object", "1")]
    [Arguments("System.IComparable", "1")]
    [Arguments("System.IComparable", "> 0")]
    [Arguments("System.Enum", "System.DayOfWeek.Monday")]
    [Arguments("string", "[_, ..]")]
    public async Task Unsupported_patterns_are_diagnosed_without_crashing(string type, string pattern)
    {
        var source = "using AlephMapper; public class Node { public object Boxed {get;set;} public void Deconstruct(out int x, out int y) { x=1; y=2; } } public static partial class Mapper { [Projectable] public static int Map(" + type + " value) => value switch { " + pattern + " => 1, _ => 0 }; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0004")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsFalse();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Synthesized_record_positional_pattern_generates_compilable_expression()
    {
        const string source = "using AlephMapper; public record Point(int X, int Y); public static partial class Mapper { [Projectable] public static int Map(Point value) => value switch { Point(var x, > 0) when x > 0 => x, _ => 0 }; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsTrue();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    [Arguments("Expression")]
    [Arguments("Map | AdaptGeneration.Expression")]
    public async Task Property_patterns_work_in_adapted_expression_paths(string flags)
    {
        var source = "using AlephMapper; public class A { public int Score {get;set;} } public class B { public int Score {get;set;} } public class D { public int Score {get;set;} } public class E { public int Score {get;set;} } " +
            "public static partial class Mapper { [Adapt(typeof(B), typeof(E), Name=\"Other\", Generate=AdaptGeneration." + flags + ")] public static D Map(A value) => new() { Score = value switch { { Score: > 0 } => 1, _ => 0 } }; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019" || d.Id == "AM0004")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("OtherExpression"))).IsTrue();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    [Arguments("value switch { 1 => throw new System.Exception(), _ => 0 }")]
    [Arguments("value is int x ? x : 0")]
    [Arguments("value switch { var x => x }")]
    public async Task Unrepresentable_expression_shapes_report_AM0019(string expression)
    {
        var (result, compilation) = await GenerateAndCompile("using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int value) => " + expression + "; }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0004")).IsFalse();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Ref_return_property_pattern_reports_AM0019()
    {
        const string source = "using AlephMapper; public class Node { private int x=1; public ref int X => ref x; } public static partial class Mapper { [Projectable] public static int Map(Node value) => value switch { { X: > 0 } => 1, _ => 0 }; }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0019")).IsTrue();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0004")).IsFalse();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("MapExpression"))).IsFalse();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Inlined_lambda_argument_conversion_is_parenthesized()
    {
        const string source = "using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int value) => Apply(value, x => x + 1); private static int Apply(int value, System.Func<int,int> f) => f(value); }";
        var (result, compilation) = await GenerateAndCompile(source);
        await Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    private static async Task<(GeneratorRunResult Result, Compilation Output)> GenerateAndCompile(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "SemanticSafety",
            [CSharpSyntaxTree.ParseText(source, parseOptions, "SemanticSafety.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Results.Single(), output);
    }

    [Test]
    public async Task New_diagnostic_descriptors_are_registered()
    {
        await Assert.That(DiagnosticDescriptors.GetById("AM0019")).IsEqualTo(DiagnosticDescriptors.UnsupportedSwitchPattern);
        await Assert.That(DiagnosticDescriptors.GetById("AM0020")).IsEqualTo(DiagnosticDescriptors.UnsupportedExtensionMember);
        await Assert.That(DiagnosticDescriptors.GetById("AM0021")).IsEqualTo(DiagnosticDescriptors.InvalidMapperDeclaration);
        await Assert.That(DiagnosticDescriptors.GetById("AM0022")).IsEqualTo(DiagnosticDescriptors.GeneratedMemberConflict);
        await Assert.That(DiagnosticDescriptors.GetById("AM0023")).IsNull();
    }

    private static async Task<GeneratorRunResult> Generate(string source) =>
        (await GenerateAndCompile(source)).Result;
}
