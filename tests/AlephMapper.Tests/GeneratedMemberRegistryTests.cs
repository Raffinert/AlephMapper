using AlephMapper.Generation;
using Microsoft.CodeAnalysis;

namespace AlephMapper.Tests;

public class GeneratedMemberRegistryTests
{
    [Test]
    [Arguments("int value", "int? value", false)]
    [Arguments("string value", "string? value", true)]
    [Arguments("dynamic value", "object value", true)]
    [Arguments("(int x, string y) value", "(int a, string b) value", true)]
    [Arguments("(int, int) value", "(int x, int y) value", true)]
    [Arguments("System.ValueTuple<int, int> value", "(int x, int y) value", true)]
    [Arguments("ref int value", "out int value", true)]
    [Arguments("ref int value", "int value", false)]
    [Arguments("System.Collections.Generic.List<T[]> value", "System.Collections.Generic.List<U[]> value", true)]
    [Arguments("T[] value", "U[,] value", false)]
    public async Task Signature_identity_follows_CSharp_overload_rules(string first, string second, bool equal)
    {
        var compilation = await ExtensionMemberTests.Compile(
            "#nullable enable\nclass A { public static void Map<T>(" + first + ") { throw null!; } } " +
            "class B { public static void Map<U>(" + second + ") { throw null!; } }");
        await Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var left = MethodSignature.FromMethod(compilation.GetTypeByMetadataName("A")!.GetMembers("Map").OfType<IMethodSymbol>().Single());
        var right = MethodSignature.FromMethod(compilation.GetTypeByMetadataName("B")!.GetMembers("Map").OfType<IMethodSymbol>().Single());
        await Assert.That(left.Equals(right)).IsEqualTo(equal);
        await Assert.That(new HashSet<MethodSignature> { left, right }.Count).IsEqualTo(equal ? 1 : 2);
        if (equal) await Assert.That(left.GetHashCode()).IsEqualTo(right.GetHashCode());
    }

    [Test]
    public async Task Unsupported_tuple_pattern_reports_AM0019_without_crashing_mapper()
    {
        var (result, output) = await ExtensionMemberTests.Generate("using AlephMapper; public static partial class Mapper { [Projectable] public static int Map((int,int) value) => value switch { (1,2) => 1, _ => 0 }; [Projectable] public static int Good(int value) => value+1; }");
        await Assert.That(result.Diagnostics.Select(d => d.Id)).IsEquivalentTo(["AM0019"]);
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("GoodExpression"))).IsTrue();
    }

    [Test]
    public async Task Failed_reservation_does_not_consume_other_members_in_batch()
    {
        var compilation = await ExtensionMemberTests.Compile("class Mapper { public static void Existing() {} public static int Value; }");
        var registry = new GeneratedMemberRegistry(compilation.GetTypeByMetadataName("Mapper")!);
        var fresh = MethodSignature.Build("Fresh", []);
        var existing = MethodSignature.Build("Existing", []);
        await Assert.That(registry.TryReserve([fresh, existing], out _)).IsFalse();
        await Assert.That(registry.TryReserve([fresh], out _)).IsTrue();
        await Assert.That(registry.TryReserve([fresh], out _)).IsFalse();
        await Assert.That(registry.TryReserve([MethodSignature.Build("Value", [])], out _)).IsFalse();
    }

    [Test]
    public async Task Classic_factory_collision_reports_diagnostic_and_preserves_other_mapping()
    {
        var (result, output) = await ExtensionMemberTests.Generate("using AlephMapper; public static partial class Mapper { [Projectable] public static int Map(int value) => value; public static int MapExpression() => 1; [Projectable] public static int Good(int value) => value+1; }");
        await Assert.That(result.Diagnostics.Any(d => d.Id == "AM0022")).IsTrue();
        await Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("GoodExpression"))).IsTrue();
    }
}
