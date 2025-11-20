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
