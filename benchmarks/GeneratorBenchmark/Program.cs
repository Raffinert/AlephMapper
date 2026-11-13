using System.Diagnostics;
using System.Text;
using AlephMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

// A reproducible baseline, not a CI performance threshold. Compilation setup
// happens outside each measurement; the recorded time is the generator driver.
const int iterations = 5;
var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
    ?.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray()
    ?? throw new InvalidOperationException("Runtime assembly references are unavailable.");

Console.WriteLine($"SDK runtime: {Environment.Version}; Roslyn: {typeof(CSharpCompilation).Assembly.GetName().Version}; iterations: {iterations}");
Console.WriteLine("scenario,iteration,elapsed_ms,allocated_bytes,generated_chars,source_step_reasons");

var mapper = CSharpSyntaxTree.ParseText(Source(1), parseOptions, "Mapper.cs");
var unrelated = CSharpSyntaxTree.ParseText("public sealed class Unrelated { public int Value => 1; }", parseOptions, "Unrelated.cs");
var compilation = CreateCompilation(mapper, unrelated);
var driver = CreateDriver();
driver = Measure("cold", 0, driver, compilation);
for (var iteration = 1; iteration <= iterations; iteration++)
{
    var next = unrelated.WithChangedText(SourceText.From($"public sealed class Unrelated {{ public int Value => {iteration + 1}; }}"));
    compilation = compilation.ReplaceSyntaxTree(unrelated, next);
    unrelated = next;
    driver = Measure("unrelated_edit", iteration, driver, compilation);
}

for (var iteration = 1; iteration <= iterations; iteration++)
{
    var next = mapper.WithChangedText(SourceText.From(Source(iteration + 1)));
    compilation = compilation.ReplaceSyntaxTree(mapper, next);
    mapper = next;
    driver = Measure("mapping_edit", iteration, driver, compilation);
}

for (var iteration = 1; iteration <= iterations; iteration++)
{
    var next = mapper.WithChangedText(SourceText.From(Source(iterations + 1, iteration + 2)));
    compilation = compilation.ReplaceSyntaxTree(mapper, next);
    mapper = next;
    driver = Measure("helper_edit", iteration, driver, compilation);
}

Measure("many_mappers_cold", 0, CreateDriver(), CreateCompilation(
    CSharpSyntaxTree.ParseText(ManyMappers(50), parseOptions, "Many.cs")));
Measure("one_large_mapper_cold", 0, CreateDriver(), CreateCompilation(
    CSharpSyntaxTree.ParseText(LargeMapper(50), parseOptions, "Large.cs")));
Measure("nested_collision_cold", 0, CreateDriver(), CreateCompilation(
    CSharpSyntaxTree.ParseText(NestedCollision(), parseOptions, "Nested.cs")));

Console.WriteLine("Times and allocations vary by machine. Compare runs with the same SDK, fixture sizes, and build configuration.");

CSharpCompilation CreateCompilation(params SyntaxTree[] trees) => CSharpCompilation.Create(
    "GeneratorBenchmark", trees, trustedAssemblies,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

CSharpGeneratorDriver CreateDriver() => CSharpGeneratorDriver.Create(
    generators: [new AlephSourceGenerator().AsSourceGenerator()],
    parseOptions: parseOptions,
    driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

CSharpGeneratorDriver Measure(string scenario, int iteration, CSharpGeneratorDriver current, CSharpCompilation input)
{
    var before = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    var next = (CSharpGeneratorDriver)current.RunGenerators(input);
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    var result = next.GetRunResult().Results.Single();
    if (result.Exception is not null || result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        throw new InvalidOperationException($"Generation failed in {scenario}", result.Exception);
    var chars = result.GeneratedSources.Sum(source => source.SourceText.Length);
    var reasons = result.TrackedSteps
        .Where(step => step.Key.EndsWith("SourceOutput", StringComparison.Ordinal))
        .SelectMany(step => step.Value)
        .SelectMany(step => step.Outputs)
        .GroupBy(output => output.Item2)
        .OrderBy(group => group.Key)
        .Select(group => $"{group.Key}:{group.Count()}");
    Console.WriteLine($"{scenario},{iteration},{stopwatch.Elapsed.TotalMilliseconds:F3},{allocated},{chars},{string.Join('|', reasons)}");
    return next;
}

static string Source(int offset, int helperMultiplier = 2) =>
    $"using AlephMapper; public static partial class Mapper {{ [Projectable] public static int Map(int value) => Helper(value) + {offset}; private static int Helper(int value) => value * {helperMultiplier}; }}";

static string ManyMappers(int count)
{
    var source = new StringBuilder("using AlephMapper;");
    for (var index = 0; index < count; index++)
        source.Append($"public static partial class Mapper{index} {{ [Projectable] public static int Map(int x) => x + {index}; }}");
    return source.ToString();
}

static string LargeMapper(int count)
{
    var source = new StringBuilder("using AlephMapper; public static partial class LargeMapper {");
    for (var index = 0; index < count; index++)
        source.Append($"[Projectable] public static int Map{index}(int x) => x + {index};");
    return source.Append('}').ToString();
}

static string NestedCollision() =>
    "using AlephMapper; public partial class First { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 1; } } " +
    "public partial class Second { public static partial class Mapper { [Projectable] public static int Map(int x) => x + 2; } }";
