using AlephMapper.Generation;
using Microsoft.CodeAnalysis;

namespace AlephMapper;

/// <summary>
/// Registers the incremental pipeline. Generation details live in the focused
/// collaborators under <c>Generation</c>.
/// </summary>
[Generator]
public sealed class AlephSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AttributeSourceEmitter.AddAttributes);

        var mappings = context.SyntaxProvider
            .CreateSyntaxProvider(MappingMethodCandidate.IsCandidate, MappingModelFactory.Create)
            .Where(static mapping => mapping != null)
            .Select(static (mapping, _) => mapping!);

        context.RegisterSourceOutput(mappings.Collect(), MapperSourceOutput.Generate);
    }
}
