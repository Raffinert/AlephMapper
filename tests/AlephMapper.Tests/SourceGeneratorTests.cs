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
