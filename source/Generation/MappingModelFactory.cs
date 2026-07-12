#nullable enable

using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AlephMapper.Generation;

internal static class MappingModelFactory
{
    public static MappingModel? Create(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration ||
            methodDeclaration.Parent is not ClassDeclarationSyntax classDeclaration ||
            !classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            return null;
        }

        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);
        if (classSymbol == null || methodSymbol == null || methodSymbol.Parameters.Length == 0)
        {
            return null;
        }

        var bodyExpression = ExtractBodyExpression(methodDeclaration);
        if (bodyExpression == null)
        {
            return null;
        }

        return new MappingModel(
            classSymbol,
            methodSymbol,
            methodSymbol.Name,
            methodSymbol.Parameters,
            methodSymbol.ReturnType,
            bodyExpression,
            semanticModel,
            HasAttribute(classSymbol, methodSymbol, typeof(ExpressiveAttribute).FullName),
            HasAttribute(classSymbol, methodSymbol, typeof(UpdatableAttribute).FullName),
            classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)),
            GetNullStrategy(methodSymbol) ?? GetNullStrategy(classSymbol) ?? NullConditionalRewrite.Ignore,
            GetCollectionPropertiesPolicy(methodSymbol) ?? GetCollectionPropertiesPolicy(classSymbol) ?? CollectionPropertiesPolicy.Skip,
            ExtractUsingDirectives(methodDeclaration),
            GetAdaptations(methodSymbol));
    }

    private static bool HasAttribute(INamedTypeSymbol classSymbol, IMethodSymbol methodSymbol, string attributeName)
    {
        return SymbolHelpers.HasAttribute(classSymbol, attributeName) ||
               SymbolHelpers.HasAttribute(methodSymbol, attributeName);
    }

    private static IReadOnlyList<AdaptationModel> GetAdaptations(IMethodSymbol methodSymbol)
    {
        var adaptations = new List<AdaptationModel>();
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != typeof(AdaptAttribute).FullName ||
                attribute.ConstructorArguments.Length < 2 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType ||
                attribute.ConstructorArguments[1].Value is not INamedTypeSymbol destinationType)
            {
                continue;
            }

            string? name = null;
            var generation = AdaptGeneration.Map | AdaptGeneration.Expression;
            var nullStrategy = NullConditionalRewrite.Ignore;
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == nameof(AdaptAttribute.Name))
                {
                    name = namedArgument.Value.Value as string;
                }
                else if (namedArgument.Key == nameof(AdaptAttribute.Generate) && namedArgument.Value.Value is int generationValue)
                {
                    generation = (AdaptGeneration)generationValue;
                }
                else if (namedArgument.Key == nameof(AdaptAttribute.NullConditionalRewrite) && namedArgument.Value.Value is int nullStrategyValue)
                {
                    nullStrategy = (NullConditionalRewrite)nullStrategyValue;
                }
            }

            adaptations.Add(new AdaptationModel(sourceType, destinationType, name, generation, nullStrategy, attribute));
        }

        return adaptations;
    }

    private static NullConditionalRewrite? GetNullStrategy(ISymbol symbol)
    {
        var value = SymbolHelpers.GetAttributeArgumentValue(
            symbol,
            typeof(ExpressiveAttribute).FullName,
            nameof(ExpressiveAttribute.NullConditionalRewrite));
        return value is int intValue ? (NullConditionalRewrite)intValue : null;
    }

    private static CollectionPropertiesPolicy? GetCollectionPropertiesPolicy(ISymbol symbol)
    {
        var value = SymbolHelpers.GetAttributeArgumentValue(
            symbol,
            typeof(UpdatableAttribute).FullName,
            nameof(UpdatableAttribute.CollectionProperties));
        return value is int intValue ? (CollectionPropertiesPolicy)intValue : null;
    }

    private static ArrowExpressionClauseSyntax? ExtractBodyExpression(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.ExpressionBody;
    }

    private static IReadOnlyList<string> ExtractUsingDirectives(SyntaxNode node)
    {
        if (node.SyntaxTree.GetRoot() is not CompilationUnitSyntax compilationUnit)
        {
            return [];
        }

        var usings = new HashSet<string>();
        foreach (var usingDirective in compilationUnit.Usings)
        {
            usings.Add(usingDirective.Name.ToString());
        }

        foreach (var namespaceDeclaration in compilationUnit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            foreach (var usingDirective in namespaceDeclaration.Usings)
            {
                usings.Add(usingDirective.Name.ToString());
            }
        }

        return usings.OrderBy(static value => value).ToList();
    }
}
