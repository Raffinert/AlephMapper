using AlephMapper.Generation.Emitters;
using AlephMapper.Helpers;
using AlephMapper.Models;
using AlephMapper.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AlephMapper.Generation;

internal static class MapperSourceOutput
{
    public static void Generate(SourceProductionContext context, ImmutableArray<MappingModel> mappings)
    {
        try
        {
            if (mappings.Length == 0)
            {
                return;
            }

            var mappingsByMethod = new Dictionary<IMethodSymbol, MappingModel>(SymbolHelpers.MethodComparer.Instance);
            var mappingsByClass = new Dictionary<INamedTypeSymbol, List<MappingModel>>(SymbolEqualityComparer.Default);
            foreach (var mapping in mappings)
            {
                mappingsByMethod[SymbolHelpers.Normalize(mapping.MethodSymbol)] = mapping;
                if (!mappingsByClass.TryGetValue(mapping.ContainingType, out var classMappings))
                {
                    classMappings = [];
                    mappingsByClass.Add(mapping.ContainingType, classMappings);
                }

                classMappings.Add(mapping);
            }

            foreach (var pair in mappingsByClass)
            {
                GenerateMapper(context, pair.Key, pair.Value, mappingsByMethod);
            }
        }
        catch (System.Exception exception)
        {
            CrashDiagnosticsReporter.Report(context, exception);
#if DEBUG
            throw;
#endif
        }
    }

    private static void GenerateMapper(
        SourceProductionContext sourceProductionContext,
        INamedTypeSymbol mapperType,
        IReadOnlyList<MappingModel> mappings,
        IDictionary<IMethodSymbol, MappingModel> mappingsByMethod)
    {
        if (!mappings.Any(static mapping =>
                (mapping.IsExpressive || mapping.IsUpdatable || mapping.Adaptations.Count > 0) && mapping.IsClassPartial))
        {
            return;
        }

        var context = new MapperGenerationContext(mapperType, mappingsByMethod, sourceProductionContext);
        foreach (var mapping in mappings)
        {
            if (!mapping.IsExpressive && !mapping.IsUpdatable && mapping.Adaptations.Count == 0)
            {
                continue;
            }

            var details = new MappingMethodDetails(mapping);
            ExpressiveMemberEmitter.Emit(details, context);
            AdaptationMemberEmitter.Emit(details, context);
            UpdatableMemberEmitter.Emit(details, context);
        }

        var generatedFile = MapperFileEmitter.Render(context);
        sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Source);
    }
}
