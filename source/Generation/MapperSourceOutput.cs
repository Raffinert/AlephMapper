#nullable enable

using AlephMapper.Diagnostics;
using AlephMapper.Generation.Emitters;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AlephMapper.Generation;

internal static class MapperSourceOutput
{
    public static void Generate(
        SourceProductionContext context,
        (MapperCandidate Left, Compilation Right) input)
    {
        try
        {
            var mapperDeclaration = FindMapperDeclaration(input.Right, input.Left, context.CancellationToken);
            if (mapperDeclaration is null)
            {
                return;
            }

            var mapperSemanticModel = input.Right.GetSemanticModel(mapperDeclaration.SyntaxTree);
            var mapperType = mapperSemanticModel.GetDeclaredSymbol(mapperDeclaration, context.CancellationToken) as INamedTypeSymbol;
            if (mapperType is null)
            {
                return;
            }

            if (!IsPrimaryMapperDeclaration(mapperType, mapperDeclaration, context.CancellationToken))
            {
                return;
            }

            var mappings = CreateMapperAnalyses(input.Right, mapperType, context.CancellationToken);
            if (mappings.Count == 0 || !mappings.Any(static mapping =>
                    (mapping.IsExpressive || mapping.IsUpdatable || mapping.Adaptations.Count > 0) && mapping.IsClassPartial))
            {
                return;
            }

            var mappingsByMethod = new Dictionary<IMethodSymbol, MappingAnalysis>(SymbolHelpers.MethodComparer.Instance);
            foreach (var mapping in mappings)
            {
                mappingsByMethod[SymbolHelpers.Normalize(mapping.MethodSymbol)] = mapping;
            }

            var catalog = new MappingCatalog(
                mappingsByMethod,
                method => CreateExternalAnalysis(input.Right, method, context.CancellationToken));
            GenerateMapper(context, mapperType, mappings, catalog);
        }
        catch (Exception exception)
        {
            CrashDiagnosticsReporter.Report(context, exception);
#if DEBUG
            throw;
#endif
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

    private static bool IsPrimaryMapperDeclaration(
        INamedTypeSymbol mapperType,
        ClassDeclarationSyntax mapperDeclaration,
        CancellationToken cancellationToken)
    {
        var primaryDeclaration = mapperType.DeclaringSyntaxReferences
            .OrderBy(static reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Span.Start)
            .Select(reference => reference.GetSyntax(cancellationToken) as ClassDeclarationSyntax)
            .FirstOrDefault(static declaration => declaration is not null &&
                MapperCandidate.IsCandidate(declaration, CancellationToken.None));

        return primaryDeclaration is not null &&
               primaryDeclaration.SyntaxTree == mapperDeclaration.SyntaxTree &&
               primaryDeclaration.Span == mapperDeclaration.Span;
    }

    private static ClassDeclarationSyntax? FindMapperDeclaration(
        Compilation compilation,
        MapperCandidate candidate,
        CancellationToken cancellationToken)
    {
        var span = new TextSpan(candidate.Start, candidate.Length);
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (!string.Equals(tree.FilePath, candidate.FilePath, StringComparison.Ordinal))
            {
                continue;
            }

            var root = tree.GetRoot(cancellationToken);
            if (span.End > root.FullSpan.End)
            {
                continue;
            }

            if (root.FindNode(span, getInnermostNodeForTie: true) is ClassDeclarationSyntax declaration &&
                declaration.Span == span)
            {
                return declaration;
            }
        }

        return null;
    }

    private static void GenerateMapper(
        SourceProductionContext sourceProductionContext,
        INamedTypeSymbol mapperType,
        IReadOnlyList<MappingAnalysis> mappings,
        MappingCatalog catalog)
    {
        var context = new MapperGenerationContext(mapperType, catalog, sourceProductionContext);
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
