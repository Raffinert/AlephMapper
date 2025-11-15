using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace AlephMapper.Tests.GeneratorTests;

internal static class AlephSourceGeneratorVerifier
{
    public static TestBuilder CreateTest(params string[] sources)
    {
        var test = new AlephSourceGeneratorTest();
        foreach (var source in sources)
        {
            test.TestState.Sources.Add(source);
        }

        return new TestBuilder(test);
    }

    internal sealed class TestBuilder(AlephSourceGeneratorTest test)
    {
        public TestBuilder ExpectGeneratedSource(string hintName, string source)
        {
            test.TestState.GeneratedSources.Add(
                (typeof(AlephSourceGenerator), hintName, SourceText.From(NormalizeLineEndings(source), Encoding.UTF8)));

            return this;
        }

        public TestBuilder ExpectAttributesSource()
        {
            return ExpectGeneratedSource(Path.GetFileName(None.GeneratorTests_Files_AlephMapper_Attributes_g_cs.GetNoneFilePath()), None.GeneratorTests_Files_AlephMapper_Attributes_g_cs.ReadAllText());
        }

        public Task RunAsync() => test.RunAsync();
    }

    internal sealed class AlephSourceGeneratorTest : CSharpSourceGeneratorTest<AlephSourceGenerator, DefaultVerifier>
    {
        public AlephSourceGeneratorTest()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60;
        }
        
        protected override ParseOptions CreateParseOptions()
        {
            var baseOptions = (CSharpParseOptions)base.CreateParseOptions();
            return baseOptions.WithLanguageVersion(LanguageVersion.Latest);
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
