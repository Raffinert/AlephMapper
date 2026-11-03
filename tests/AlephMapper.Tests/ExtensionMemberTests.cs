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

    internal static async Task<CSharpCompilation> Compile(string source)
    {
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        return CSharpCompilation.Create("ExtensionTests" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
