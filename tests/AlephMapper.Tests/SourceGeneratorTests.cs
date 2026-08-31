using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;
using AlephMapper.Generation;
using System.Diagnostics;
using System.Text;

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

    [Test]
    public async Task UnrelatedMethodsAreRejectedBySyntaxCandidateFilter()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "public class Unrelated { public int Add(int left, int right) => left + right; }",
            _parseOptions);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        await Assert.That(MappingMethodCandidate.IsCandidate(method, CancellationToken.None)).IsFalse();
    }

    [Test]
    public async Task MapperHelpersRemainCandidatesForInlining()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int value) => Helper(value); public static int Helper(int value) => value; }",
            _parseOptions);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();

        await Assert.That(methods).Count().IsEqualTo(2);
        await Assert.That(MappingMethodCandidate.IsCandidate(methods.Single(method => method.Identifier.ValueText == "Helper"), CancellationToken.None)).IsTrue();
    }

    [Test]
    public async Task ClassLevelConfigurationAcrossPartialDeclarationsGeneratesOnce()
    {
        const string configuration = """
            using AlephMapper;
            namespace Fixture;
            [Projectable]
            public static partial class Mapper { }
            """;
        const string mapping = """
            namespace Fixture;
            public static partial class Mapper
            {
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            public sealed class Source { public int Value { get; set; } }
            public sealed class Target { public int Value { get; set; } }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "PartialMapper",
            [
                CSharpSyntaxTree.ParseText(configuration, _parseOptions, "Configuration.cs"),
                CSharpSyntaxTree.ParseText(mapping, _parseOptions, "Mapping.cs")
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);
        var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var generatorResult = updatedDriver.GetRunResult().Results.Single();

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generatorResult.Exception).IsNull();
        await Assert.That(generatorResult.GeneratedSources
            .Count(source => source.HintName.EndsWith("Mapper_GeneratedMappings.g.cs", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratedConfigurationTypesAreNotPublic()
    {
        var assembly = typeof(AlephSourceGenerator).Assembly;
        var typeNames = new[]
        {
            "ProjectableAttribute",
            "UpdatableAttribute",
            "AdaptAttribute",
            "NullConditionalRewrite",
            "CollectionPropertiesPolicy",
            "AdaptGeneration"
        };

        foreach (var typeName in typeNames)
        {
            var type = assembly.GetType("AlephMapper." + typeName);
            await Assert.That(type).IsNotNull();
            await Assert.That(type!.IsPublic).IsFalse();
        }

        await Assert.That(assembly.GetType("AlephMapper.ExpressiveAttribute")).IsNull();
    }

    [Test]
    public async Task EmbeddedConfigurationTypesDoNotConflictAcrossConsumerAssemblies()
    {
        const string projectASource = """
            using AlephMapper;
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectB")]
            namespace ProjectA;
            public static partial class Mapper
            {
                [Projectable]
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            public sealed class Source { public int Value { get; set; } }
            public sealed class Target { public int Value { get; set; } }
            """;
        const string projectBSource = """
            using AlephMapper;
            using ProjectA;
            namespace ProjectB;
            public static partial class Mapper
            {
                [Projectable]
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var projectA = CSharpCompilation.Create(
            "ProjectA",
            [CSharpSyntaxTree.ParseText(projectASource, _parseOptions, "ProjectA.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var projectADriver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);
        projectADriver.RunGeneratorsAndUpdateCompilation(projectA, out var projectAOutput, out var projectADiagnostics);
        await Assert.That(projectADiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(projectAOutput.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        using var projectAImage = new MemoryStream();
        var emitResult = projectAOutput.Emit(projectAImage);
        await Assert.That(emitResult.Success).IsTrue();

        var projectB = CSharpCompilation.Create(
            "ProjectB",
            [CSharpSyntaxTree.ParseText(projectBSource, _parseOptions, "ProjectB.cs")],
            references.Add(MetadataReference.CreateFromImage(projectAImage.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var projectBDriver = CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions);
        projectBDriver.RunGeneratorsAndUpdateCompilation(projectB, out var projectBOutput, out var projectBDiagnostics);

        await Assert.That(projectBDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(projectBOutput.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(projectBOutput.GetTypeByMetadataName("AlephMapper.ProjectableAttribute")!.DeclaredAccessibility)
            .IsEqualTo(Accessibility.Internal);
        await Assert.That(projectBOutput.GetTypeByMetadataName("AlephMapper.ProjectableAttribute")!.GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Microsoft.CodeAnalysis.EmbeddedAttribute"))
            .IsTrue();
    }

    [Test]
    public async Task AttributeDiscoveryGeneratesOneFileForCombinedConfiguration()
    {
        const string source = """
            using AlephMapper;
            namespace Fixture;
            public static partial class Mapper
            {
                [Projectable]
                [Updatable]
                [Adapt(typeof(AdaptedSource), typeof(AdaptedTarget), Name = "MapAdapted")]
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            public sealed class Source { public int Value { get; set; } }
            public sealed class Target { public int Value { get; set; } }
            public sealed class AdaptedSource { public int Value { get; set; } }
            public sealed class AdaptedTarget { public int Value { get; set; } }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "CombinedConfiguration",
            [CSharpSyntaxTree.ParseText(source, _parseOptions, "Mapper.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var result = driver.GetRunResult().Results.Single();

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources
            .Count(generated => generated.HintName.EndsWith("Mapper_GeneratedMappings.g.cs", StringComparison.Ordinal)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task AttributeCandidatesRemainUnchangedWhenOnlyUnrelatedSourceChanges()
    {
        const string mapperA = """
            using AlephMapper;
            namespace Fixture;
            public static partial class MapperA
            {
                [Projectable]
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            public sealed class Source { public int Value { get; set; } }
            public sealed class Target { public int Value { get; set; } }
            """;
        const string mapperB = """
            using AlephMapper;
            namespace Fixture;
            public static partial class MapperB
            {
                [Projectable]
                public static Target Map(Source source) => new() { Value = source.Value };
            }
            """;
        const string unrelated = "namespace Fixture; public sealed class Unrelated { public int Value => 1; }";

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var mapperATree = CSharpSyntaxTree.ParseText(mapperA, _parseOptions, "MapperA.cs");
        var mapperBTree = CSharpSyntaxTree.ParseText(mapperB, _parseOptions, "MapperB.cs");
        var unrelatedTree = CSharpSyntaxTree.ParseText(unrelated, _parseOptions, "Unrelated.cs");
        var compilation = CSharpCompilation.Create(
            "IncrementalTracking",
            [mapperATree, mapperBTree, unrelatedTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CreateTrackingDriver().RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var updatedUnrelatedTree = unrelatedTree.WithChangedText(SourceText.From(
            "namespace Fixture; public sealed class Unrelated { public int Value => 2; }"));
        var updatedCompilation = compilation.ReplaceSyntaxTree(unrelatedTree, updatedUnrelatedTree);
        driver = driver.RunGeneratorsAndUpdateCompilation(updatedCompilation, out _, out _);

        var trackedSteps = driver.GetRunResult().Results.Single().TrackedSteps;
        var candidateOutputs = trackedSteps["AlephMapper.ProjectableGenerationResult"]
            .SelectMany(static step => step.Outputs)
            .ToArray();

        await Assert.That(candidateOutputs).Count().IsEqualTo(2);
        await Assert.That(candidateOutputs.All(static output => output.Item2 == IncrementalStepRunReason.Unchanged)).IsTrue();
    }

    [Test]
    public async Task LargeCompilationDiscoversOnlyAttributedMappers()
    {
        var source = new StringBuilder("using AlephMapper; namespace Fixture; public sealed class Source { public int Value { get; set; } } public sealed class Target { public int Value { get; set; } } public sealed class Unrelated {");
        for (var index = 0; index < 1_000; index++)
        {
            source.Append("public int Method").Append(index).Append("() => ").Append(index).Append(';');
        }

        source.Append('}');
        for (var index = 0; index < 10; index++)
        {
            source.Append("public static partial class Mapper").Append(index)
                .Append(" { [Projectable] public static Target Map(Source source) => new() { Value = source.Value }; }");
        }

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "LargeDiscovery",
            [CSharpSyntaxTree.ParseText(source.ToString(), _parseOptions, "Large.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stopwatch = Stopwatch.StartNew();
        var driver = CreateTrackingDriver().RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        stopwatch.Stop();

        var result = driver.GetRunResult().Results.Single();
        var candidateOutputs = result.TrackedSteps["AlephMapper.ProjectableGenerationResult"]
            .SelectMany(static step => step.Outputs)
            .ToArray();

        Console.WriteLine($"Large discovery completed in {stopwatch.ElapsedMilliseconds} ms.");
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(candidateOutputs).Count().IsEqualTo(10);
        await Assert.That(result.GeneratedSources
            .Count(generated => generated.HintName.EndsWith("_GeneratedMappings.g.cs", StringComparison.Ordinal)))
            .IsEqualTo(10);
    }

    private CSharpGeneratorDriver CreateTrackingDriver()
    {
        return CSharpGeneratorDriver.Create(
            generators: [new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: _parseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
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
            var testCaseName = Path.GetFileName(testCaseGroup.Key);
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

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics);

        await Assert.That(generatorDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var result = driver.GetRunResult().Results.Single();

        var actualSources = result.GeneratedSources.ToDictionary(
            source => Path.GetFileName(source.HintName),
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

    [Test]
    public async Task AdaptedOutputCompilesForImplicitObjectCreation()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
                public static PersonDto MapPerson(Person source) => new() { Name = source.Name };
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; }
            public sealed class Employee { public string Name { get; set; } = string.Empty; }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Name { get; set; } = string.Empty; }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "AdaptedOutput",
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        await Assert.That(generatorDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(driver.GetRunResult().Results.Single().Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task AdaptedOutputCompilesForGenericNestedMapper()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Outer<TOuter>
            {
                public static partial class Mapper<TMapper>
                {
                    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
                    public static PersonDto MapPerson(Person source) => new() { Name = source.Name };
                }
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; }
            public sealed class Employee { public string Name { get; set; } = string.Empty; }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Name { get; set; } = string.Empty; }
            """;

        await AssertAdaptedOutputCompiles(source, "GenericNestedMapper");
    }

    [Test]
    public async Task AdaptedOutputAcceptsOptionalAndParamsConstructors()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
                public static PersonDto MapPerson(Person source) => new();

                [Adapt(typeof(Employee), typeof(EmployeeParamsDto), Name = "MapEmployeeParams")]
                public static PersonParamsDto MapPersonParams(Person source) => new PersonParamsDto(source.Id, source.Id);
            }

            public sealed class Person { public int Id { get; set; } }
            public sealed class Employee { public int Id { get; set; } }
            public sealed class PersonDto { public PersonDto() { } }
            public sealed class EmployeeDto { public EmployeeDto(int version = 1) { } }
            public sealed class PersonParamsDto { public PersonParamsDto(params int[] ids) { } }
            public sealed class EmployeeParamsDto { public EmployeeParamsDto(params int[] ids) { } }
            """;

        await AssertAdaptedOutputCompiles(source, "OptionalAndParamsConstructors");
    }

    [Test]
    public async Task AdaptedOutputAcceptsSourceInstanceMethodCalls()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
                public static PersonDto MapPerson(Person source) => new() { Name = source.Name.Trim() };
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; }
            public sealed class Employee { public string Name { get; set; } = string.Empty; }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Name { get; set; } = string.Empty; }
            """;

        await AssertAdaptedOutputCompiles(source, "InstanceMethodCall");
    }

    [Test]
    public async Task AdaptedOutputSupportsNullConditionalRewrite()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static PersonDto MapPerson(Person source) => new() { Street = source.Address?.Street ?? "Unknown" };
            }

            public sealed class Person { public Address? Address { get; set; } }
            public sealed class Employee { public Address? Address { get; set; } }
            public sealed class Address { public string Street { get; set; } = string.Empty; }
            public sealed class PersonDto { public string Street { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Street { get; set; } = string.Empty; }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "NullConditionalAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapEmployeeExpression"));

        await Assert.That(generatedSource).DoesNotContain("?.");
        await Assert.That(generatedSource).Contains("source.Address != null");
    }

    [Test]
    public async Task AdaptedOutputSupportsNullConditionalRewriteWithExtensionInlining()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public static partial class AddressMapper
            {
                public static AddressDto ToDto(this Address source) => new() { Street = source.Street };
            }

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static PersonDto MapPerson(Person source) => new() { Address = source.Address?.ToDto() };
            }

            public sealed class Person { public Address? Address { get; set; } }
            public sealed class Employee { public Address? Address { get; set; } }
            public sealed class Address { public string Street { get; set; } = string.Empty; }
            public sealed class PersonDto { public AddressDto? Address { get; set; } }
            public sealed class EmployeeDto { public AddressDto? Address { get; set; } }
            public sealed class AddressDto { public string Street { get; set; } = string.Empty; }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "NullConditionalExtensionAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapEmployeeExpression"));

        await Assert.That(generatedSource).DoesNotContain("?.");
        await Assert.That(generatedSource).DoesNotContain(".ToDto(");
        await Assert.That(generatedSource).Contains("source.Address != null");
        await Assert.That(generatedSource).Contains("Street = source.Address.Street");
    }

    [Test]
    public async Task AdaptedOutputSupportsNullConditionalRootExtensionInlining()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", Generate = AdaptGeneration.Expression, NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static PersonDto MapPerson(Person source) => source?.MapNonNullPerson();

                private static PersonDto MapNonNullPerson(this Person source) => new()
                {
                    Name = source.Name,
                    Tax = new TaxInfo { Rate = source.TaxRate }
                };
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; public decimal TaxRate { get; set; } }
            public sealed class Employee { public string Name { get; set; } = string.Empty; public decimal TaxRate { get; set; } }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; public TaxInfo Tax { get; set; } = new(); }
            public sealed class EmployeeDto { public string Name { get; set; } = string.Empty; public EmployeeTaxInfo Tax { get; set; } = new(); }
            public sealed class TaxInfo { public decimal Rate { get; set; } }
            public sealed class EmployeeTaxInfo { public decimal Rate { get; set; } }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "NullConditionalRootExtensionAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapEmployeeExpression"));

        await Assert.That(generatedSource).DoesNotContain("?.");
        await Assert.That(generatedSource).DoesNotContain("MapNonNullPerson");
        await Assert.That(generatedSource).Contains("source != null");
        await Assert.That(generatedSource).Contains("new EmployeeDto");
        await Assert.That(generatedSource).Contains("Tax = new EmployeeTaxInfo");
    }

    [Test]
    public async Task AdaptedOutputSupportsComplexNullConditionalRootExtensionInlining()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public static partial class OrderItemMapper
            {
                [Adapt(typeof(OrderItemInput), typeof(ReadOnlyOrderItem), Generate = AdaptGeneration.Expression, Name = "MapInputToReadOnlyItem", NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static OrderItem MapInputToItem(this OrderItemInput source) => source?.MapNonNullInputToItem();

                private static OrderItem MapNonNullInputToItem(this OrderItemInput source) => new()
                {
                    ProductName = source.ProductName,
                    CurrencyCode = source.CurrencyCode,
                    UnitPrice = source.UnitPrice,
                    Quantity = source.Quantity,
                    Subtotal = source.Subtotal.Round(2),
                    Sequence = source.Sequence,
                    Sku = source.Sku,
                    DiscountAmount = source.Discount,
                    TaxPercentage = source.TaxRate,
                    IsArchived = false,
                    TotalAmount = (source.Subtotal * (1m + source.TaxRate / 100m)).Round(2),
                    OrderNumber = source.OrderNumber,
                    OrderLineNumber = source.OrderLineNumber,
                    Tax = new TaxInfo
                    {
                        Rate = source.TaxRate,
                        Amount = (source.Subtotal * source.TaxRate / 100m).Round(2)
                    }
                };

                private static decimal Round(this decimal value, int decimals) => decimal.Round(value, decimals);
            }

            public sealed class OrderItemInput
            {
                public string ProductName { get; set; } = string.Empty;
                public string CurrencyCode { get; set; } = string.Empty;
                public decimal UnitPrice { get; set; }
                public decimal Quantity { get; set; }
                public decimal Subtotal { get; set; }
                public int Sequence { get; set; }
                public string Sku { get; set; } = string.Empty;
                public decimal Discount { get; set; }
                public decimal TaxRate { get; set; }
                public string OrderNumber { get; set; } = string.Empty;
                public string OrderLineNumber { get; set; } = string.Empty;
            }

            public sealed class OrderItem
            {
                public string ProductName { get; set; } = string.Empty;
                public string CurrencyCode { get; set; } = string.Empty;
                public decimal UnitPrice { get; set; }
                public decimal Quantity { get; set; }
                public decimal Subtotal { get; set; }
                public int Sequence { get; set; }
                public string Sku { get; set; } = string.Empty;
                public decimal DiscountAmount { get; set; }
                public decimal TaxPercentage { get; set; }
                public bool IsArchived { get; set; }
                public decimal TotalAmount { get; set; }
                public string OrderNumber { get; set; } = string.Empty;
                public string OrderLineNumber { get; set; } = string.Empty;
                public TaxInfo Tax { get; set; } = new();
            }

            public sealed class ReadOnlyOrderItem
            {
                public string ProductName { get; set; } = string.Empty;
                public string CurrencyCode { get; set; } = string.Empty;
                public decimal UnitPrice { get; set; }
                public decimal Quantity { get; set; }
                public decimal Subtotal { get; set; }
                public int Sequence { get; set; }
                public string Sku { get; set; } = string.Empty;
                public decimal DiscountAmount { get; set; }
                public decimal TaxPercentage { get; set; }
                public bool IsArchived { get; set; }
                public decimal TotalAmount { get; set; }
                public string OrderNumber { get; set; } = string.Empty;
                public string OrderLineNumber { get; set; } = string.Empty;
                public ReadOnlyTaxInfo Tax { get; set; } = new();
            }

            public sealed class TaxInfo
            {
                public decimal Rate { get; set; }
                public decimal Amount { get; set; }
            }

            public sealed class ReadOnlyTaxInfo
            {
                public decimal Rate { get; set; }
                public decimal Amount { get; set; }
            }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "ComplexNullConditionalRootExtensionAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapInputToReadOnlyItemExpression"));

        await Assert.That(generatedSource).DoesNotContain("?.");
        await Assert.That(generatedSource).DoesNotContain("MapNonNullInputToItem");
        await Assert.That(generatedSource).Contains("source != null");
        await Assert.That(generatedSource).Contains("new ReadOnlyOrderItem");
        await Assert.That(generatedSource).Contains("Tax = new ReadOnlyTaxInfo");
        await Assert.That(generatedSource).Contains("TotalAmount = decimal.Round((source.Subtotal * (1m + source.TaxRate / 100m)), 2)");
    }

    [Test]
    public async Task AdaptedOutputDoesNotApplyOuterAssignmentTypeInsideLambdas()
    {
        const string source = """
            using AlephMapper;
            using System.Collections.Generic;
            using System.Linq;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", Generate = AdaptGeneration.Expression)]
                public static PersonDto MapPerson(Person source, string preferredLanguage, string fallbackLanguage) => new()
                {
                    Description = MapDescription(source.Value, preferredLanguage, fallbackLanguage)
                };

                private static string MapDescription(Value value, string preferredLanguage, string fallbackLanguage) =>
                    value.Descriptions
                        .Select(description => description.ToDescriptionWithOrder(preferredLanguage, fallbackLanguage))
                        .Where(description => description.Order < 3)
                        .OrderBy(description => description.Order)
                        .Select(description => description.Description)
                        .FirstOrDefault() ?? value.Code;

                private static DescriptionWithOrder ToDescriptionWithOrder(this Description description, string preferredLanguage, string fallbackLanguage) =>
                    new()
                    {
                        Order = description.Language == preferredLanguage ? 1 : description.Language == fallbackLanguage ? 2 : 3,
                        Description = description.Text
                    };

                private sealed class DescriptionWithOrder
                {
                    public int Order { get; set; }
                    public string Description { get; set; } = string.Empty;
                }
            }

            public sealed class Person { public Value Value { get; set; } = new(); }
            public sealed class Employee { public Value Value { get; set; } = new(); }
            public sealed class PersonDto { public string Description { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Description { get; set; } = string.Empty; }
            public sealed class Value { public string Code { get; set; } = string.Empty; public List<Description> Descriptions { get; set; } = []; }
            public sealed class Description { public string Language { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "LambdaOuterAssignmentTypeAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapEmployeeExpression"));

        await Assert.That(generatedSource).DoesNotContain("new string");
        await Assert.That(generatedSource).Contains("new DescriptionWithOrder");
    }

    [Test]
    public async Task AdaptedOutputFormatsAnonymousObjectsInsideFluentChains()
    {
        const string source = """
            using AlephMapper;
            using System.Collections.Generic;
            using System.Linq;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee", Generate = AdaptGeneration.Expression)]
                public static PersonDto MapPerson(Person source, string preferredLanguage, string fallbackLanguage) => new()
                {
                    Description = MapDescription(source.Value, preferredLanguage, fallbackLanguage)
                };

                private static string MapDescription(Value value, string preferredLanguage, string fallbackLanguage) =>
                    value.Descriptions
                        .Select(description => new
                        {
                            Order = description.Language == preferredLanguage ? 1 : description.Language == fallbackLanguage ? 2 : 3,
                            Description = description.Text
                        })
                        .Where(description => description.Order < 3)
                        .OrderBy(description => description.Order)
                        .Select(description => description.Description)
                        .FirstOrDefault() ?? value.Code;
            }

            public sealed class Person { public Value Value { get; set; } = new(); }
            public sealed class Employee { public Value Value { get; set; } = new(); }
            public sealed class PersonDto { public string Description { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Description { get; set; } = string.Empty; }
            public sealed class Value { public string Code { get; set; } = string.Empty; public List<Description> Descriptions { get; set; } = []; }
            public sealed class Description { public string Language { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "AnonymousObjectFluentChainAdapt");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("MapEmployeeExpression"));

        await Assert.That(generatedSource).DoesNotContain("new\r\n        {");
        await Assert.That(generatedSource).DoesNotContain("new\n        {");
        await Assert.That(generatedSource).Contains(".Select(description => new");
        await Assert.That(generatedSource).Contains("Order = description.Language == preferredLanguage");
    }

    [Test]
    public async Task ExpressionOutputFormatsLogicalConditionChains()
    {
        const string source = """
            using AlephMapper;
            using System;

            namespace Fixture;

            public static partial class Criteria
            {
                [Projectable]
                public static bool HasCategoryLine(InvoiceLine line, Guid dataSetId, Guid invoiceId) =>
                    line.Invoice.DataSetId == dataSetId &&
                    line.Invoice.InvoiceIdReference == invoiceId &&
                    line.CategoryId != null;

                [Projectable]
                public static bool HasCategoryLineSingleLine(InvoiceLine line, Guid dataSetId, Guid invoiceId) =>
                    line.Invoice.DataSetId == dataSetId && line.Invoice.InvoiceIdReference == invoiceId && line.CategoryId != null;
            }

            public sealed class InvoiceLine
            {
                public Invoice Invoice { get; set; } = new();
                public Guid? CategoryId { get; set; }
            }

            public sealed class Invoice
            {
                public Guid DataSetId { get; set; }
                public Guid InvoiceIdReference { get; set; }
            }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "LogicalConditionChainExpression");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("HasCategoryLineExpression"));

        await Assert.That(generatedSource).Contains("line => line.Invoice.DataSetId == dataSetId");
        await Assert.That(generatedSource).Contains($"{Environment.NewLine}            && line.Invoice.InvoiceIdReference == invoiceId");
        await Assert.That(generatedSource).Contains($"{Environment.NewLine}            && line.CategoryId != null;");
        await Assert.That(generatedSource).Contains("line => line.Invoice.DataSetId == dataSetId && line.Invoice.InvoiceIdReference == invoiceId && line.CategoryId != null;");
    }

    [Test]
    public async Task ExpressionOutputPreservesConditionalLayoutIncludingLoweredSwitches()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Projectable]
                public static string SingleConditional(bool condition) => condition ? "yes" : "no";

                [Projectable]
                public static string MultilineConditional(bool condition) =>
                    condition
                        ? "yes"
                        : "no";

                [Projectable]
                public static string SingleSwitch(int value) => value switch { 1 => "one", _ => "other" };

                [Projectable]
                public static string MultilineSwitch(int value) =>
                    value switch
                    {
                        1 => "one",
                        _ => "other"
                    };
            }
            """;

        var generatedSources = await AssertAdaptedOutputCompiles(source, "ConditionalExpressionFormatting");
        var generatedSource = generatedSources.Single(sourceText => sourceText.Contains("SingleConditionalExpression"));

        await Assert.That(generatedSource).Contains("condition => condition ? \"yes\" : \"no\";");
        await Assert.That(generatedSource).Contains(
            $"condition => condition{Environment.NewLine}            ? \"yes\"{Environment.NewLine}            : \"no\";");
        await Assert.That(generatedSource).Contains("value => value == 1 ? \"one\" : \"other\";");
        await Assert.That(generatedSource).Contains(
            $"value => value == 1{Environment.NewLine}            ? \"one\"{Environment.NewLine}            : \"other\";");
    }

    [Test]
    public async Task AdaptReportsIncompatibleDirectMemberAssignment()
    {
        const string source = """
            using AlephMapper;

            namespace Fixture;

            public static partial class Mapper
            {
                [Adapt(typeof(Employee), typeof(EmployeeDto), Name = "MapEmployee")]
                public static PersonDto MapPerson(Person source) => new() { Name = source.Name };
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; }
            public sealed class Employee { public int Name { get; set; } }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; }
            public sealed class EmployeeDto { public string Name { get; set; } = string.Empty; }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "IncompatibleAdaptation",
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var diagnostics = driver.GetRunResult().Results.Single().Diagnostics;
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "AM0008")).IsTrue();
    }

    [Test]
    public async Task AdaptationDiagnosticsAreReported()
    {
        var cases = new Dictionary<string, string>
        {
            ["AM0005"] = CreateAdaptationSource("[Adapt(typeof(Person), typeof(PersonDto), Name = \"MapPersonCopy\")]", "new() { Name = source.Name }"),
            ["AM0006"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new() { Name = source.Name }", employeeMembers: "public int Id { get; set; }"),
            ["AM0007"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new() { Name = source.Name }", employeeDtoMembers: "public int Id { get; set; }"),
            ["AM0008"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new() { Name = source.Name }", employeeMembers: "public int Name { get; set; }"),
            ["AM0009"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new() { Name = source.Name }", additionalMethods: "public static EmployeeDto MapEmployee(Employee source) => new();"),
            ["AM0010"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "null"),
            ["AM0011"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Generate = AdaptGeneration.Expression)]", "new() { Name = source.Name }"),
            ["AM0012"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]\n    [Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployeeAgain\")]", "new() { Name = source.Name }"),
            ["AM0013"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "Loop(source)", additionalMethods: "private static PersonDto Loop(Person source) => MapPerson(source);"),
            ["AM0014"] = CreateAdaptationSource("[Adapt(typeof(List<>), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new() { Name = source.Name }", additionalUsings: "using System.Collections.Generic;"),
            ["AM0015"] = CreateAdaptationSource("[Adapt(typeof(Employee), typeof(EmployeeDto), Name = \"MapEmployee\")]", "new PersonDto(source.Id)", employeeMembers: "public string Name { get; set; } = string.Empty; public int Id { get; set; }", personDtoConstructor: "public PersonDto(int id) { Name = id.ToString(); }", employeeDtoConstructor: "public EmployeeDto() { }")
        };

        foreach (var testCase in cases)
        {
            await AssertAdaptationDiagnostic(testCase.Value, testCase.Key);
        }
    }

    [Test]
    public async Task UnsafeNullConditionalReceiverReportsDiagnosticAndSkipsExpression()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public sealed class Address
            {
                public string Street { get; set; } = string.Empty;
                public Address? GetNested() => this;
            }

            public sealed class AddressDto
            {
                public string Street { get; set; } = string.Empty;
            }

            public static class AddressMapper
            {
                public static AddressDto ToDto(this Address source) =>
                    new() { Street = source.Street };
            }

            public static partial class Mapper
            {
                [Projectable(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
                public static AddressDto? Map(Address source) =>
                    source.GetNested()?.ToDto();
            }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "UnsafeNullConditionalReceiver",
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult().Results.Single();

        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "AM0016")).IsTrue();
        await Assert.That(result.GeneratedSources.Any(generated =>
            generated.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task NonePolicyConditionalExtensionReportsDiagnosticAndSkipsExpression()
    {
        const string source = """
            #nullable enable
            using AlephMapper;

            namespace Fixture;

            public sealed class Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public sealed class AddressDto
            {
                public string Street { get; set; } = string.Empty;
            }

            public static class AddressMapper
            {
                public static AddressDto ToDto(this Address source) =>
                    new() { Street = source.Street };
            }

            public static partial class Mapper
            {
                [Projectable(NullConditionalRewrite = NullConditionalRewrite.None)]
                public static AddressDto? Map(Address? source) =>
                    source?.ToDto();
            }
            """;

        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "NonePolicyConditionalExtension",
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var result = driver.GetRunResult().Results.Single();

        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "AM0017")).IsTrue();
        await Assert.That(result.GeneratedSources.Any(generated =>
            generated.SourceText.ToString().Contains("MapExpression", StringComparison.Ordinal))).IsFalse();
        await Assert.That(outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    private async Task AssertAdaptationDiagnostic(string source, string diagnosticId)
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            diagnosticId,
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var diagnostics = driver.GetRunResult().Results.Single().Diagnostics;
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId)).IsTrue();
    }

    private async Task<IReadOnlyList<string>> AssertAdaptedOutputCompiles(string source, string assemblyName)
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, _parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = _driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        await Assert.That(generatorDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(driver.GetRunResult().Results.Single().Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        return driver.GetRunResult().Results.Single().GeneratedSources
            .Select(sourceText => sourceText.SourceText.ToString())
            .ToArray();
    }

    private static string CreateAdaptationSource(
        string attribute,
        string body,
        string employeeMembers = "public string Name { get; set; } = string.Empty;",
        string employeeDtoMembers = "public string Name { get; set; } = string.Empty;",
        string additionalMethods = "",
        string additionalUsings = "",
        string personDtoConstructor = "public PersonDto() { }",
        string employeeDtoConstructor = "public EmployeeDto() { }")
    {
        return $$"""
            using AlephMapper;
            {{additionalUsings}}

            namespace Fixture;

            public static partial class Mapper
            {
                {{attribute}}
                public static PersonDto MapPerson(Person source) => {{body}};
                {{additionalMethods}}
            }

            public sealed class Person { public string Name { get; set; } = string.Empty; public int Id { get; set; } }
            public sealed class Employee { {{employeeMembers}} }
            public sealed class PersonDto { public string Name { get; set; } = string.Empty; {{personDtoConstructor}} }
            public sealed class EmployeeDto { {{employeeDtoMembers}} {{employeeDtoConstructor}} }
            """;
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
