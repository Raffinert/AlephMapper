using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Text;

namespace AlephMapper.Generation;

internal static class AttributeSourceEmitter
{
    public static void AddAttributes(IncrementalGeneratorPostInitializationContext context)
    {
        var assembly = typeof(AlephSourceGenerator).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream("AlephMapper.Attributes.cs")!);
        context.AddSource("AlephMapper.Attributes.g.cs", SourceText.From(reader.ReadToEnd(), Encoding.UTF8));
    }
}
