#nullable enable

using AlephMapper.Diagnostics;
using AlephMapper.Generation.Emitters;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AlephMapper.Generation;

internal static class MapperSourceOutput
{
    public static MapperGenerationResult Create(
        GeneratorAttributeSyntaxContext attributeContext,
        MapperAttributeKind attributeKind,
        CancellationToken cancellationToken)
    {
        try
        {
            var mapperDeclaration = attributeContext.TargetNode switch
            {
                ClassDeclarationSyntax classDeclaration => classDeclaration,
                MethodDeclarationSyntax { Parent: ClassDeclarationSyntax classDeclaration } => classDeclaration,
                _ => null
            };
            var mapperType = attributeContext.TargetSymbol switch
            {
                INamedTypeSymbol classSymbol => classSymbol,
                IMethodSymbol methodSymbol => methodSymbol.ContainingType,
                _ => null
            };
            if (mapperDeclaration is null || mapperType is null)
            {
                return MapperGenerationResult.Empty;
            }

            var compilation = attributeContext.SemanticModel.Compilation;
            var candidate = MapperCandidate.Create(attributeContext, attributeKind, cancellationToken);
            if (!IsPrimaryMapperCandidate(compilation, mapperType, candidate, cancellationToken))
            {
                return MapperGenerationResult.Empty;
            }

            var mappings = CreateMapperAnalyses(compilation, mapperType, cancellationToken);
            if (mappings.Count == 0 || !mappings.Any(static mapping =>
                    (mapping.IsProjectable || mapping.IsUpdatable || mapping.Adaptations.Count > 0) && mapping.IsClassPartial))
            {
                return MapperGenerationResult.Empty;
            }

            var mappingsByMethod = new Dictionary<IMethodSymbol, MappingAnalysis>(SymbolHelpers.MethodComparer.Instance);
            foreach (var mapping in mappings)
            {
                mappingsByMethod[SymbolHelpers.Normalize(mapping.MethodSymbol)] = mapping;
            }

            var catalog = new MappingCatalog(
                mappingsByMethod,
                method => CreateExternalAnalysis(compilation, method, cancellationToken));
            return GenerateMapper(mapperType, mappings, catalog);
        }
        catch (Exception exception)
        {
            return new MapperGenerationResult(
                null,
                null,
                [CrashDiagnosticsReporter.CreateDiagnostic(exception)]);
#if DEBUG
            throw;
#endif
        }
    }

    public static void Emit(SourceProductionContext context, MapperGenerationResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (result.HintName is not null && result.Source is not null)
        {
            context.AddSource(result.HintName, result.Source);
        }
    }

    private static IReadOnlyList<MappingAnalysis> CreateMapperAnalyses(
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var mappings = new List<MappingAnalysis>();
        foreach (var declarationReference in mapperType.DeclaringSyntaxReferences
                     .OrderBy(static reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
                     .ThenBy(static reference => reference.Span.Start))
        {
            if (declarationReference.GetSyntax(cancellationToken) is not ClassDeclarationSyntax declaration)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>()
                         .Where(static method => method.ExpressionBody is not null)
                         .OrderBy(static method => method.SpanStart))
            {
                var mapping = MappingAnalysisFactory.Create(semanticModel, method, cancellationToken);
                if (mapping is not null)
                {
                    mappings.Add(mapping);
                }
            }
        }

        return mappings
            .OrderBy(static mapping => mapping.MethodSymbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
            .ThenBy(static mapping => mapping.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static MappingAnalysis? CreateExternalAnalysis(
        Compilation compilation,
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        foreach (var declarationReference in method.DeclaringSyntaxReferences
                     .OrderBy(static reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
                     .ThenBy(static reference => reference.Span.Start))
        {
            if (declarationReference.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration ||
                declaration.ExpressionBody is null ||
                !MappingMethodCandidate.IsCandidate(declaration, cancellationToken))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            return MappingAnalysisFactory.Create(semanticModel, declaration, cancellationToken);
        }

        return null;
    }

    private static bool IsPrimaryMapperCandidate(
        Compilation compilation,
        INamedTypeSymbol mapperType,
        MapperCandidate candidate,
        CancellationToken cancellationToken)
    {
        var primaryCandidate = mapperType.DeclaringSyntaxReferences
            .OrderBy(static reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Span.Start)
            .Select(reference => reference.GetSyntax(cancellationToken) as ClassDeclarationSyntax)
            .Where(static declaration => declaration is not null)
            .SelectMany(declaration => GetMapperCandidates(compilation, declaration!, cancellationToken))
            .OrderBy(static current => current.FilePath, StringComparer.Ordinal)
            .ThenBy(static current => current.Start)
            .ThenBy(static current => current.Length)
            .ThenBy(static current => current.AttributeKind)
            .FirstOrDefault();

        return primaryCandidate is not null && primaryCandidate.Equals(candidate);
    }

    private static IEnumerable<MapperCandidate> GetMapperCandidates(
        Compilation compilation,
        ClassDeclarationSyntax mapperDeclaration,
        CancellationToken cancellationToken)
    {
        var semanticModel = compilation.GetSemanticModel(mapperDeclaration.SyntaxTree);

        if (ContainsAlephMapperAttribute(mapperDeclaration.AttributeLists, semanticModel, cancellationToken, out var classKind))
        {
            yield return CreateCandidate(mapperDeclaration, classKind);
        }

        foreach (var method in mapperDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (ContainsAlephMapperAttribute(method.AttributeLists, semanticModel, cancellationToken, out var methodKind))
            {
                yield return CreateCandidate(method, methodKind);
            }
        }
    }

    private static bool ContainsAlephMapperAttribute(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out MapperAttributeKind attributeKind)
    {
        attributeKind = MapperAttributeKind.Adapt;
        foreach (var attribute in attributeLists.SelectMany(static list => list.Attributes))
        {
            var type = semanticModel.GetTypeInfo(attribute, cancellationToken).Type;
            switch (type?.ToDisplayString())
            {
                case "AlephMapper.ProjectableAttribute":
                    attributeKind = MapperAttributeKind.Projectable;
                    return true;
                case "AlephMapper.UpdatableAttribute":
                    attributeKind = MapperAttributeKind.Updatable;
                    return true;
                case "AlephMapper.AdaptAttribute":
                    attributeKind = MapperAttributeKind.Adapt;
                    return true;
            }
        }

        return false;
    }

    private static MapperCandidate CreateCandidate(SyntaxNode targetNode, MapperAttributeKind attributeKind)
    {
        return new MapperCandidate(
            targetNode.SyntaxTree.FilePath,
            targetNode.SpanStart,
            targetNode.Span.Length,
            attributeKind);
    }

    private static MapperGenerationResult GenerateMapper(
        INamedTypeSymbol mapperType,
        IReadOnlyList<MappingAnalysis> mappings,
        MappingCatalog catalog)
    {
        var context = new MapperGenerationContext(mapperType, catalog);
        foreach (var mapping in mappings)
        {
            if (!mapping.IsProjectable && !mapping.IsUpdatable && mapping.Adaptations.Count == 0)
            {
                continue;
            }

            var details = new MappingMethodDetails(mapping);
            ProjectableMemberEmitter.Emit(details, context);
            AdaptationMemberEmitter.Emit(details, context);
            UpdatableMemberEmitter.Emit(details, context);
        }

        var generatedFile = MapperFileEmitter.Render(context);
        return new MapperGenerationResult(generatedFile.HintName, generatedFile.Source, context.GetDiagnostics());
    }
}
