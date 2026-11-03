using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;

namespace AlephMapper.Tests;

public class ExtensionMemberTests
{
    [Test]
    public async Task CSharp14_extension_receiver_is_separate_from_method_parameters()
    {
        var source = "public class Order { public int Total {get;set;} } public static class Ext { extension(Order order) { public int DoubleTotal => order.Total*2; public int Scale(int factor) => order.Total*factor; } public static int Use(Order o) => o.DoubleTotal + o.Scale(3); }";
        var compilation = await Compile(source);
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var block = tree.GetRoot().DescendantNodes().OfType<ExtensionBlockDeclarationSyntax>().Single();
        var type = (INamedTypeSymbol)model.GetDeclaredSymbol(block)!;
        await Assert.That(type.IsExtension).IsTrue();
        await Assert.That(type.ExtensionParameter!.Name).IsEqualTo("order");
        var method = (IMethodSymbol)model.GetDeclaredSymbol(block.Members.OfType<MethodDeclarationSyntax>().Single())!;
        await Assert.That(method.Parameters.Length).IsEqualTo(1);
        await Assert.That(method.Parameters[0].Name).IsEqualTo("factor");
        var access = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().First(n => n.Name.Identifier.ValueText == "DoubleTotal");
        await Assert.That(SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access).Symbol,
            model.GetDeclaredSymbol(block.Members.OfType<PropertyDeclarationSyntax>().Single()))).IsTrue();
    }

    [Test]
    [Arguments("public int Double => n*2;", "DoubleExpression().Compile()(3) == 6")]
    [Arguments("public int Scale(int factor) => n*factor;", "ScaleExpression(4).Compile()(3) == 12")]
    public async Task CSharp14_extension_factories_compile_and_match_source(string declaration, string check)
    {
        var source = "using AlephMapper; using System.Linq; public static partial class Ext { extension(int n) { [Projectable] " + declaration + " } public static bool Check() => " + check + "; }";
        var (result, output) = await Generate(source);
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        await Assert.That(emit.Success).IsTrue();
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        await Assert.That((bool)assembly.GetType("Ext")!.GetMethod("Check")!.Invoke(null, null)!).IsTrue();
    }

    [Test]
    [Arguments("public int Value { get { return n; } }")]
    [Arguments("public int Value() { return n; }")]
    [Arguments("public static int Value => 3;")]
    [Arguments("public int Value(int x = 1) => n+x;")]
    [Arguments("public T Value<T>(T x) => x;")]
    [Arguments("public int Value() => System.Linq.Enumerable.Sum(new[]{n}, x => { return x; });")]
    [Arguments("public System.Collections.Generic.IEnumerable<int> Value => from x in new[]{1} select x+n;")]
    public async Task Unsupported_extension_members_report_AM0021_and_isolate_failure(string declaration)
    {
        var source = "using AlephMapper; using System.Linq; public static partial class Ext { extension(int n) { [Projectable] " + declaration + " [Projectable] public int Good => n+1; } }";
        var (result, output) = await Generate(source);
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0021")).IsTrue();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0004")).IsFalse();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("GoodExpression"))).IsTrue();
    }

    [Test]
    [Arguments("public int A => n.B; public int B => n.A;")]
    [Arguments("public int A() => n.B; public int B => n.A();")]
    public async Task Extension_member_cycles_terminate_with_diagnostic(string declarations)
    {
        var (result, output) = await Generate("using AlephMapper; [Projectable] public static partial class Ext { extension(int n) { " + declarations + " public int Good => n+1; } }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0002")).IsTrue();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0004")).IsFalse();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("GoodExpression"))).IsTrue();
    }

    [Test]
    public async Task Extension_factory_signature_conflicts_are_diagnosed()
    {
        var (result, output) = await Generate("using AlephMapper; [Projectable] public static partial class Ext { extension(int n) { public int Value => n; } extension(string n) { public int Value => n.Length; } }");
        await Assert.That(result.Diagnostics.Count(d => d.Id == "AM0021")).IsEqualTo(2);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Extension_members_in_partial_files_generate_once_and_deterministically()
    {
        const string source = "using AlephMapper; [Projectable] public static partial class Ext { extension(int n) { public int Double => n*2; } } // file boundary\nusing AlephMapper; public static partial class Ext { extension(int n) { [Projectable] public int Triple => n*3; } }";
        var (first, output) = await Generate(source);
        var (second, _) = await Generate(source);
        await Assert.That(first.Diagnostics).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var generated = first.GeneratedSources.Where(s => s.HintName.EndsWith("GeneratedMappings.g.cs")).ToArray();
        await Assert.That(generated.Length).IsEqualTo(1);
        await Assert.That(generated[0].SourceText.ToString()).IsEqualTo(second.GeneratedSources.Single(s => s.HintName == generated[0].HintName).SourceText.ToString());
    }

    [Test]
    public async Task AM0021_is_registered_and_AM0020_remains_absent()
    {
        await Assert.That(AlephMapper.Diagnostics.DiagnosticDescriptors.GetById("AM0021")).IsNotNull();
        await Assert.That(AlephMapper.Diagnostics.DiagnosticDescriptors.GetById("AM0020")).IsNull();
    }

    [Test]
    public async Task Escaped_receiver_and_factory_argument_names_compile()
    {
        var (result, output) = await Generate("using AlephMapper; public static partial class Ext { extension(int @event) { [Projectable] public int Value(int @class) => @event + @class; } }");
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    [Arguments("private static int Offset => 3;", "Offset")]
    [Arguments("private static int Offset() { return 3; }", "Offset()")]
    [Arguments("private static int Offset => 3;", "Ext.Offset")]
    [Arguments("private static int Offset() { return 3; }", "Ext.Offset()")]
    public async Task Inaccessible_extension_dependencies_are_diagnosed(string declaration, string use)
    {
        var (result, output) = await Generate("using AlephMapper; public static partial class Ext { " + declaration + " extension(int n) { [Projectable] public int Value => n + " + use + "; } } public static partial class Mapper { [Projectable] public static int Map(int n) => n.Value; }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0021")).IsTrue();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Unsupported_extension_update_request_is_not_silently_ignored()
    {
        var (result, _) = await Generate("using AlephMapper; public static partial class Ext { extension(int n) { [Updatable] public int Value() => n+1; } }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0021")).IsTrue();
    }

    [Test]
    public async Task Bound_extension_method_groups_report_diagnostic()
    {
        var (result, output) = await Generate("using AlephMapper; using System.Linq; public static partial class Ext { extension(string n) { public int Add(int x) => n.Length+x; } [Projectable] public static int[] Map(int[] input) => input.Select(string.Empty.Add).ToArray(); }");
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0021")).IsTrue();
    }

    [Test]
    public async Task Extension_members_resolve_by_symbol_across_namespaces()
    {
        const string source = """
            using AlephMapper;
            using A;
            using B;
            namespace A {
                public class Left { public int Number {get;set;} }
                public static partial class Ext { extension(Left n) { [Projectable] public int Value => n.Number*2; } }
            }
            namespace B {
                public class Right { public int Number {get;set;} }
                public static partial class Ext { extension(Right n) { [Projectable] public int Value => n.Number*3; } }
            }
            public class Input { public Left Left {get;set;} public Right Right {get;set;} }
            public static partial class Mapper { [Projectable] public static int Map(Input input) => input.Left.Value + input.Right.Value; }
            """;
        var (result, output) = await Generate(source);
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Single(s => s.HintName == "Mapper_GeneratedMappings.g.cs").SourceText.ToString())
            .DoesNotContain(".Value");
    }

    [Test]
    public async Task NullConditional_None_keeps_its_diagnostic_for_extension_members()
    {
        var (result, output) = await Generate("using AlephMapper; public class Node {} public static partial class Ext { extension(Node n) { public int Score => 1; } [Projectable(NullConditionalRewrite=NullConditionalRewrite.None)] public static int? Map(Node n) => n?.Score; }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0017")).IsTrue();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Missing_partial_outer_class_has_actionable_diagnostic()
    {
        var (result, output) = await Generate("using AlephMapper; public static class Ext { extension(int n) { [Projectable] public int Value => n+1; } }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0021" && d.GetMessage().Contains("partial"))).IsTrue();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    internal static async Task<(GeneratorRunResult Result, Compilation Output)> Generate(string source)
    {
        var compilation = await Compile(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new AlephSourceGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Results.Single(), output);
    }

    internal static async Task<CSharpCompilation> Compile(string source)
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        return CSharpCompilation.Create("ExtensionTests" + Guid.NewGuid().ToString("N"),
            source.Split("// file boundary").Select((text, index) => CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14), path: "source" + index + ".cs")), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
