using Microsoft.CodeAnalysis;

namespace AlephMapper.Tests.GeneratorTests;

public class AlephSourceGeneratorTests
{
    [Test]
    public async Task Attributes_are_emitted_even_without_mappers()
    {
        var source = await None.GeneratorTests_Files_AnySources_Source_cs.ReadAllTextAsync();

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource()
            .RunAsync();
    }

    [Test]
    public async Task Expressive_mappers_receive_expression_companions()
    {
        var source = await None.GeneratorTests_Files_Expressive_Source_cs.ReadAllTextAsync();

        var expected = Nones.GetMatches("*/**/Expressive/Expected/*.cs")
            .Select(m => new { FileName = Path.GetFileName(m.GetNoneFilePath()), Content = m.ReadAllText() })
            .ToArray();

        var verifier = AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource();

        foreach (var generated in expected)
        {
            verifier.ExpectGeneratedSource(generated.FileName, generated.Content);
        }

        await verifier.RunAsync();
    }

    [Test]
    public async Task Null_conditional_rewrite_respects_nullable_disabled_context()
    {
        var source = await None.GeneratorTests_Files_NullableDisabled_Source_cs.ReadAllTextAsync();
        var expected = await None.GeneratorTests_Files_NullableDisabled_Expected_Tests_NullableDisabledMapper_GeneratedMappings_g_cs.ReadAllTextAsync();

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .WithNullableContext(NullableContextOptions.Disable)
            .ExpectAttributesSource()
            .ExpectGeneratedSource("Tests_NullableDisabledMapper_GeneratedMappings.g.cs", expected)
            .RunAsync();
    }

    [Test]
    public async Task Null_conditional_rewrite_emits_nullable_annotations_when_enabled()
    {
        var source = await None.GeneratorTests_Files_NullableEnabled_Source_cs.ReadAllTextAsync();
        var expected = await None.GeneratorTests_Files_NullableEnabled_Expected_Tests_NullableEnabledMapper_GeneratedMappings_g_cs.ReadAllTextAsync();

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource()
            .ExpectGeneratedSource("Tests_NullableEnabledMapper_GeneratedMappings.g.cs", expected)
            .WithNullableContext(NullableContextOptions.Enable)
            .RunAsync();
    }

    [Test]
    public async Task Updatable_mappers_generate_update_helpers()
    {
        var source = await None.GeneratorTests_Files_Updatable_Source_cs.ReadAllTextAsync();

        var expected = Nones.GetMatches("*/**/Updatable/Expected/*.cs")
            .Select(m => new { FileName = Path.GetFileName(m.GetNoneFilePath()), Content = m.ReadAllText() } )
            .ToArray();

        var verifier = AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource();

        foreach (var generated in expected) 
        {
            verifier.ExpectGeneratedSource(generated.FileName, generated.Content);
        }

        await verifier.RunAsync();
    }
}
