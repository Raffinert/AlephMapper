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
