#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        var rewritten = (ConditionalExpressionSyntax?)base.VisitConditionalExpression(node);
        if (rewritten == null)
        {
            return null;
        }

        var annotationKind = HasMultilineConditionalLayout(node)
            ? GeneratedSyntaxAnnotations.MultilineConditional
            : GeneratedSyntaxAnnotations.SingleLineConditional;
        return rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(annotationKind));
    }

    private static bool HasMultilineConditionalLayout(ConditionalExpressionSyntax node)
    {
        var text = node.ToFullString();
        return text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0;
    }
}

#nullable restore
