#nullable enable

using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (CanQuerySemanticModel(node) && model.GetSymbolInfo(node).Symbol is IPropertySymbol property && RoslynCompatibility.IsExtensionType(property.ContainingType))
            return RewriteExtensionProperty(node, property, node.Expression);
        if (CanQuerySemanticModel(node) && model.GetSymbolInfo(node).Symbol is IMethodSymbol method &&
            RoslynCompatibility.IsExtensionType(method.ContainingType) && !(node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node))
            return UnsupportedExtension(node, method);
        if (_extensionExpansion && CanQuerySemanticModel(node) && model.GetSymbolInfo(node).Symbol is { } dependency)
        {
            if (dependency is IFieldSymbol { IsConst: true } field && field.Type.TypeKind != TypeKind.Enum)
                return ExtensionConstant(field).WithTriviaFrom(node);
            if (_outputType is not null && !model.Compilation.IsSymbolAccessibleWithin(dependency, _outputType))
                _unsupportedExtensions.Add((dependency, "an expanded body references an inaccessible member"));
        }
        var rewritten = (MemberAccessExpressionSyntax?)base.VisitMemberAccessExpression(node);
        if (rewritten == null ||
            !CanQuerySemanticModel(node) ||
            model.GetSymbolInfo(node).Symbol is not ISymbol member ||
            !member.IsStatic ||
            member.ContainingType is not ITypeSymbol containingType)
        {
            return rewritten;
        }

        var typeName = TypeDisplay.ForSymbol(
            containingType,
            containingType.NullableAnnotation,
            nullablePolicy);
        var typeSyntax = ParseTypeName(typeName).WithTriviaFrom(rewritten.Expression);
        return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeSyntax,
                rewritten.Name)
            .WithTriviaFrom(rewritten);
    }
}

#nullable restore
