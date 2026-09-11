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
