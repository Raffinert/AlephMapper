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

internal static class MappingAnalysisFactory
{
    public static MappingAnalysis? Create(
        SemanticModel semanticModel,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        if (methodDeclaration.Parent is not ClassDeclarationSyntax classDeclaration ||
            !classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            return null;
        }

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

        return new MappingAnalysis(
            classSymbol,
            methodSymbol,
            methodSymbol.Name,
            methodSymbol.Parameters,
            methodSymbol.ReturnType,
            bodyExpression,
            semanticModel,
            HasAttribute(classSymbol, methodSymbol, typeof(ProjectableAttribute).FullName),
            HasAttribute(classSymbol, methodSymbol, typeof(UpdatableAttribute).FullName),
            classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)),
            GetNullStrategy(methodSymbol) ?? GetNullStrategy(classSymbol) ?? NullConditionalRewrite.Ignore,
            GetCollectionPropertiesPolicy(methodSymbol) ?? GetCollectionPropertiesPolicy(classSymbol) ?? CollectionPropertiesPolicy.Skip,
            ExtractUsingDirectives(methodDeclaration),
            GetAdaptations(methodSymbol));
    }

    internal static string? ExtensionUnsupportedReason(ISymbol member, bool requirePartial = true)
    {
        var type = member.ContainingType;
        var receiver = type.ExtensionParameter;
        if (member.IsStatic || receiver is null) return "static members or unnamed receivers are unsupported";
        if (type.TypeParameters.Length != 0 || member is IMethodSymbol { IsGenericMethod: true })
            return "generic extension blocks and methods are not supported yet";
        if (receiver.RefKind != RefKind.None || receiver.Type.IsRefLikeType)
            return "ref/in/out or ref-like receivers cannot be projected";
        if (requirePartial && !type.ContainingType.DeclaringSyntaxReferences.All(r => r.GetSyntax() is ClassDeclarationSyntax c &&
                c.Modifiers.Any(SyntaxKind.StaticKeyword) && c.Modifiers.Any(SyntaxKind.PartialKeyword)))
            return "the outer class must be static partial";
        if (member is IMethodSymbol method && (method.ReturnsVoid || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.Parameters.Any(p => p.RefKind != RefKind.None || p.IsOptional || p.IsParams)))
            return "void/ref returns, ref parameters, optional parameters and params are unsupported";
        if (member is IPropertySymbol property && (property.ReturnsByRef || property.ReturnsByRefReadonly || property.SetMethod is not null))
            return "only value-returning getter-only properties are supported";
        var syntax = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is not MethodDeclarationSyntax { ExpressionBody: not null } and
            not PropertyDeclarationSyntax { ExpressionBody: not null })
            return "an expression body is required";
        if (syntax.DescendantNodes().OfType<QueryExpressionSyntax>().Any())
            return "query syntax is unsupported in extension bodies; use LINQ method syntax";
        if (syntax.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => lambda.Body is BlockSyntax || lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword)))
            return "block-bodied or async anonymous functions cannot be projected";
        return null;
    }

    internal static MappingAnalysis? CreateExtension(SemanticModel model, SyntaxNode syntax, CancellationToken token)
    {
        if (syntax.Parent is not ExtensionBlockDeclarationSyntax || model.GetDeclaredSymbol(syntax, token) is not { } member ||
            ExtensionUnsupportedReason(member, SymbolHelpers.HasAttribute(member, typeof(ProjectableAttribute).FullName) ||
                SymbolHelpers.HasAttribute(member.ContainingType.ContainingType, typeof(ProjectableAttribute).FullName)) is not null) return null;
        var receiver = member.ContainingType.ExtensionParameter!;
        var outer = member.ContainingType.ContainingType;
        var method = member as IMethodSymbol ?? ((IPropertySymbol)member).GetMethod!;
        var body = syntax is MethodDeclarationSyntax m ? m.ExpressionBody! : ((PropertyDeclarationSyntax)syntax).ExpressionBody!;
        return new MappingAnalysis(outer, method, member.Name,
            new[] { receiver }.Concat(method.Parameters).ToArray(), method.ReturnType, body, model,
            SymbolHelpers.HasAttribute(member, typeof(ProjectableAttribute).FullName) || SymbolHelpers.HasAttribute(outer, typeof(ProjectableAttribute).FullName),
            false, true, GetNullStrategy(member) ?? GetNullStrategy(outer) ?? NullConditionalRewrite.Ignore,
            CollectionPropertiesPolicy.Skip, ExtractUsingDirectives(syntax), [], member, receiver);
    }

    private static bool HasAttribute(INamedTypeSymbol classSymbol, IMethodSymbol methodSymbol, string attributeName)
    {
        return SymbolHelpers.HasAttribute(classSymbol, attributeName) ||
               SymbolHelpers.HasAttribute(methodSymbol, attributeName);
    }

    private static IReadOnlyList<AdaptationAnalysis> GetAdaptations(IMethodSymbol methodSymbol)
    {
        var adaptations = new List<AdaptationAnalysis>();
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

            adaptations.Add(new AdaptationAnalysis(
                sourceType,
                destinationType,
                name,
                generation,
                nullStrategy,
                SourceLocationModel.FromSyntax(attribute.ApplicationSyntaxReference)));
        }

        return adaptations;
    }

    private static NullConditionalRewrite? GetNullStrategy(ISymbol symbol)
    {
        var value = SymbolHelpers.GetAttributeArgumentValue(
            symbol,
            typeof(ProjectableAttribute).FullName,
            nameof(ProjectableAttribute.NullConditionalRewrite));
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
            AddUsing(usings, usingDirective);
        }

        foreach (var namespaceDeclaration in compilationUnit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            foreach (var usingDirective in namespaceDeclaration.Usings)
            {
                AddUsing(usings, usingDirective);
            }
        }

        return usings.OrderBy(static value => value).ToList();
    }

    private static void AddUsing(ISet<string> usings, UsingDirectiveSyntax usingDirective)
    {
        if (usingDirective.Name is { } name)
        {
            usings.Add(name.ToString());
        }
    }
}
