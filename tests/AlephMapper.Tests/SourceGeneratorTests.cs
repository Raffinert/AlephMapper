using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.Tests;

public class SourceGeneratorTests
{
    private readonly CSharpParseOptions _parseOptions;
    private readonly CSharpGeneratorDriver _driver;

    public SourceGeneratorTests()
    {
        _parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var generator = new AlephSourceGenerator().AsSourceGenerator();
        _driver = CSharpGeneratorDriver.Create(generators: [generator], parseOptions: _parseOptions);
    }

    public static IEnumerable<object[]> GetTestCases()
    {
        var groupedByTestCase = Nones.GetMatches("Files/**/*.cs")
            .GroupBy(n => n.GetNoneFilePath()
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .SkipWhile(x => x != "Files")
                .Take(2)
                .Last());

        foreach (var testCaseGroup in groupedByTestCase)
        {
            var testCaseName = Path.GetFileName(testCaseGroup.Key)!;
            var sourceFiles = testCaseGroup
                .Where(n => n.GetNoneFilePath().Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipWhile(x => x != "Files").Take(3).Last() == "Sources")
                .Select(n => n.GetNoneFilePath())
                .OrderBy(n => n)
                .ToArray();

            var expectedFiles = testCaseGroup
                .Where(n => n.GetNoneFilePath().Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipWhile(x => x != "Files").Take(3).Last() == "Expected")
                .Select(n => n.GetNoneFilePath())
                .OrderBy(n => n)
                .ToArray();

            yield return
            [
                testCaseName,
                sourceFiles,
                expectedFiles
            ];

        }
    }

    [Test]
    [MethodDataSource(typeof(SourceGeneratorTests), nameof(GetTestCases))]
    public async Task GenerationMatchesBaseLine(string name, string[] sourceFiles, string[] expectedFiles)
    {
        var sourceTrees = await Task.WhenAll(sourceFiles.Select(async sourceFile => CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(sourceFile), _parseOptions, sourceFile)));

        var globalUsings = CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.Linq;
            global using System.Linq.Expressions;
            global using System.Threading;
            global using System.Threading.Tasks;
            """,
            _parseOptions,
            "GlobalUsings.g.cs");

        var syntaxTrees = sourceTrees.Append(globalUsings).ToArray();

        var references = (await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None))
            .Add(MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location));

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var compilation = CSharpCompilation.Create(
            assemblyName: "AllTests",
            syntaxTrees,
            references,
            compilationOptions);

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var result = driver.GetRunResult().Results.Single();

        var actualSources = result.GeneratedSources.ToDictionary(
            source => Path.GetFileName(source.HintName)!,
            source => NormalizeLineEndings(source.SourceText.ToString()),
            StringComparer.Ordinal);

        var expectedFileContents = expectedFiles
            .ToDictionary(
                file =>
                {
                    var fileName = Path.GetFileName(file);
                    return fileName ?? throw new InvalidOperationException($"Unable to get file name for expected file '{file}'.");
                },
                file => NormalizeLineEndings(File.ReadAllText(file)),
                StringComparer.Ordinal);
        
        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_BASELINE"), "1", StringComparison.Ordinal))
        {
            var expectedRoot = Path.GetDirectoryName(expectedFiles.First())
                               ?? throw new InvalidOperationException("Unable to locate expected folder.");

            foreach (var generated in actualSources)
            {
                var filePath = Path.Combine(expectedRoot, generated.Key);
                var absolutePath = Path.Combine(Path.GetFullPath(@"..\..\..\"), filePath);
                await File.WriteAllTextAsync(absolutePath, generated.Value);
            }

            expectedFileContents = new Dictionary<string, string>(actualSources, StringComparer.Ordinal);
        }

        foreach (var expected in expectedFileContents)
        {
            await Assert.That(actualSources.ContainsKey(expected.Key)).IsTrue();
            var actual = actualSources[expected.Key];
            if (!string.Equals(actual, expected.Value, StringComparison.Ordinal))
            {
                Console.WriteLine($"Mismatch detected for {expected.Key}");
            }

            await Assert.That(actual).IsEqualTo(expected.Value);
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
