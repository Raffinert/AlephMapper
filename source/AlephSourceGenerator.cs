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

        var projectableMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(ProjectableAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperSourceOutput.Create(
                attributeContext,
                MapperAttributeKind.Projectable,
                cancellationToken));

        var updatableMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(UpdatableAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperSourceOutput.Create(
                attributeContext,
                MapperAttributeKind.Updatable,
                cancellationToken));

        var adaptableMappers = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(AdaptAttribute).FullName,
            MapperCandidate.IsAttributeTarget,
            static (attributeContext, cancellationToken) => MapperSourceOutput.Create(
                attributeContext,
                MapperAttributeKind.Adapt,
                cancellationToken));

        RegisterMapperOutput(context, projectableMappers, "Projectable");
        RegisterMapperOutput(context, updatableMappers, "Updatable");
        RegisterMapperOutput(context, adaptableMappers, "Adapt");
    }

    private static void RegisterMapperOutput(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MapperGenerationResult> mapperResults,
        string configurationKind)
    {
        context.RegisterSourceOutput(
            mapperResults
                .WithTrackingName($"AlephMapper.{configurationKind}Candidates")
                .WithTrackingName($"AlephMapper.{configurationKind}GenerationResult")
                .Combine(context.CompilationProvider),
            MapperSourceOutput.Emit);
    }
}
