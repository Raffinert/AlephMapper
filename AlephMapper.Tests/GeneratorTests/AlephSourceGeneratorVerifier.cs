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
            return ExpectGeneratedSource("AlephMapper.Attributes.g.cs", AttributesSource);
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

    private const string AttributesSource =
"""
using System;

namespace AlephMapper;

/// <summary>
/// Configures how null-conditional operators are handled
/// </summary>
public enum NullConditionalRewrite
{
    /// <summary>
    /// Don't rewrite null conditional operators (Default behavior).
    /// Usage of null conditional operators is thereby not allowed
    /// </summary>
    None,

    /// <summary>
    /// Ignore null-conditional operators in the generated expression tree
    /// </summary>
    /// <remarks>
    /// <c>(A?.B)</c> is rewritten as expression: <c>(A.B)</c>
    /// </remarks>
    Ignore,

    /// <summary>
    /// Translates null-conditional operators into explicit null checks
    /// </summary>
    /// <remarks>
    /// <c>(A?.B)</c> is rewritten as expression: <c>(A != null ? A.B : null)</c>
    /// </remarks>
    Rewrite
}

/// <summary>
/// Marks a class to generate expressive companion methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExpressiveAttribute : Attribute
{
    /// <summary>
    /// Get or set how null-conditional operators are handled
    /// </summary>
    public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
}

/// <summary>
/// Marks a class to generate update companion methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class UpdatableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the policy for handling collection updates during mapping operations
    /// </summary>
    public CollectionPropertiesPolicy CollectionProperties { get; set; } = CollectionPropertiesPolicy.Skip;
}

/// <summary>
/// Defines the policy for handling collection updates during mapping operations
/// </summary>
public enum CollectionPropertiesPolicy
{
    /// <summary>
    /// Skip collection updates - collections will not be modified during mapping
    /// </summary>
    Skip,

    /// <summary>
    /// Update collections - collections will be updated during mapping operations
    /// </summary>
    Update
}
""";
}
