#nullable enable

using AlephMapper.Generation.Emitters;
using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using AlephMapper.Diagnostics;

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
                MethodDeclarationSyntax or PropertyDeclarationSyntax when RoslynCompatibility.IsExtensionMember(attributeContext.TargetNode) => attributeContext.TargetNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault(),
                _ => null
            };
            var mapperType = attributeContext.TargetSymbol switch
            {
                INamedTypeSymbol classSymbol => classSymbol,
                IMethodSymbol methodSymbol => RoslynCompatibility.IsExtensionType(methodSymbol.ContainingType) ? methodSymbol.ContainingType.ContainingType : methodSymbol.ContainingType,
                IPropertySymbol propertySymbol when RoslynCompatibility.IsExtensionType(propertySymbol.ContainingType) => propertySymbol.ContainingType.ContainingType,
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

            if (!MapperDeclarationValidator.IsEligible(mapperType))
            {
                var descriptor = mapperType.GetTypeMembers().Any(RoslynCompatibility.IsExtensionType)
                    ? DiagnosticDescriptors.UnsupportedExtensionMember : DiagnosticDescriptors.InvalidMapperDeclaration;
                return new MapperGenerationResult(null, null, ImmutableArray.Create(GenerationDiagnostic.From(Diagnostic.Create(
                    descriptor, mapperDeclaration.Identifier.GetLocation(), mapperType.Name, MapperDeclarationValidator.RequiredDeclaration))));
            }

            var mappings = CreateMapperAnalyses(compilation, mapperType, cancellationToken);
            if (mappings.Count == 0 && !mapperType.GetTypeMembers().Any(RoslynCompatibility.IsExtensionType))
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
#if DEBUG
        catch
        {
            throw;
        }
#else
        catch (Exception exception)
        {
            return new MapperGenerationResult(
                null,
                null,
                ImmutableArray.Create(CrashDiagnosticsReporter.CreateDiagnostic(exception)));
        }
#endif
    }

    public static void EmitSource(
        SourceProductionContext context,
        MapperSourceResult result)
    {
        if (result.HintName is not null && result.Source is not null)
        {
            context.AddSource(result.HintName, result.Source);
        }
    }

    public static void EmitDiagnostics(
        SourceProductionContext context,
        MapperGenerationResult result,
        Compilation compilation)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic(compilation));
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
            foreach (var member in RoslynCompatibility.GetExtensionMembers(declaration))
            {
                var extension = MappingAnalysisFactory.CreateExtension(semanticModel, member, cancellationToken);
                if (extension is not null) mappings.Add(extension);
            }
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
        if (RoslynCompatibility.IsExtensionType(method.ContainingType))
        {
            var symbol = method.AssociatedSymbol ?? (ISymbol)method;
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax(cancellationToken);
                return MappingAnalysisFactory.CreateExtension(compilation.GetSemanticModel(syntax.SyntaxTree), syntax, cancellationToken);
            }
            return null;
        }
        foreach (var declarationReference in method.DeclaringSyntaxReferences
                     .OrderBy(static reference => reference.SyntaxTree.FilePath, StringComparer.Ordinal)
                     .ThenBy(static reference => reference.Span.Start))
        {
            if (declarationReference.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration ||
                declaration.ExpressionBody is null)
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

        foreach (var member in RoslynCompatibility.GetExtensionMembers(mapperDeclaration))
        {
            var attributes = member switch { MethodDeclarationSyntax m => m.AttributeLists, PropertyDeclarationSyntax p => p.AttributeLists, _ => default };
            if (ContainsAlephMapperAttribute(attributes, semanticModel, cancellationToken, out var kind))
                yield return CreateCandidate(member, kind);
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
        foreach (var extensionType in mapperType.GetTypeMembers().Where(RoslynCompatibility.IsExtensionType))
        foreach (var member in extensionType.GetMembers().Where(s => s is IPropertySymbol || s is IMethodSymbol { MethodKind: MethodKind.Ordinary }))
        {
            var requested = SymbolHelpers.HasAttribute(member, typeof(ProjectableAttribute).FullName) ||
                SymbolHelpers.HasAttribute(member, typeof(UpdatableAttribute).FullName) ||
                SymbolHelpers.HasAttribute(member, typeof(AdaptAttribute).FullName) ||
                SymbolHelpers.HasAttribute(mapperType, typeof(ProjectableAttribute).FullName);
            if (!requested) continue;
            var reason = MappingAnalysisFactory.ExtensionUnsupportedReason(member);
            if (reason is not null) context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedExtensionMember, member.Locations.FirstOrDefault(), member.Name, reason));
        }
        var conflictingProjections = new HashSet<MethodSignature>(mappings.Where(m => m.IsProjectable)
            .GroupBy(m => MethodSignature.Build(m.Name + "Expression", m.Parameters.Skip(1).Select(p => p.Type), m.MethodSymbol.Arity))
            .Where(group => group.Count() > 1 && group.Any(m => m.ReceiverSymbol is not null)).Select(group => group.Key));
        foreach (var mapping in mappings)
        {
            if (!mapping.IsProjectable && !mapping.IsUpdatable && mapping.Adaptations.Count == 0)
            {
                continue;
            }

            if (mapping.IsProjectable && conflictingProjections.Contains(MethodSignature.Build(
                mapping.Name + "Expression", mapping.Parameters.Skip(1).Select(p => p.Type), mapping.MethodSymbol.Arity)))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedExtensionMember,
                    mapping.MemberSymbol.Locations.FirstOrDefault(), mapping.Name, "the generated factory signature conflicts with another mapping"));
                continue;
            }
            var details = new MappingMethodDetails(mapping);
            ProjectableMemberEmitter.Emit(details, context);
            AdaptationMemberEmitter.Emit(details, context);
            UpdatableMemberEmitter.Emit(details, context);
        }

        if (context.Members.Length == 0 && mapperType.GetTypeMembers().Any(RoslynCompatibility.IsExtensionType))
            return new MapperGenerationResult(null, null, context.GetDiagnostics());
        var generatedFile = MapperFileEmitter.Render(context);
        return new MapperGenerationResult(generatedFile.HintName, generatedFile.Source, context.GetDiagnostics());
    }
}
