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

        var mapperCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                MapperCandidate.IsCandidate,
                static (syntaxContext, cancellationToken) => MapperCandidate.Create(syntaxContext.Node, cancellationToken));

        context.RegisterSourceOutput(
            mapperCandidates.Combine(context.CompilationProvider),
            MapperSourceOutput.Generate);
    }
}
