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
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddEmbeddedAttributeDefinition();
            AttributeSourceEmitter.AddAttributes(postInitializationContext);
        });

        var expressiveMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(ExpressiveAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperCandidate.Create(
                attributeContext,
                MapperAttributeKind.Expressive,
                cancellationToken));

        var updatableMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(UpdatableAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperCandidate.Create(
                attributeContext,
                MapperAttributeKind.Updatable,
                cancellationToken));

        var adaptableMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(AdaptAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperCandidate.Create(
                attributeContext,
                MapperAttributeKind.Adapt,
                cancellationToken));

        RegisterMapperOutput(context, expressiveMappers, "Expressive");
        RegisterMapperOutput(context, updatableMappers, "Updatable");
        RegisterMapperOutput(context, adaptableMappers, "Adapt");
    }

    private static void RegisterMapperOutput(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MapperCandidate> mapperCandidates,
        string configurationKind)
    {
        context.RegisterSourceOutput(
            mapperCandidates
                .WithTrackingName($"AlephMapper.{configurationKind}Candidates")
                .Combine(context.CompilationProvider)
                .WithTrackingName($"AlephMapper.{configurationKind}GenerationInput"),
            MapperSourceOutput.Generate);
    }
}
