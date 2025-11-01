using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AlephMapper.SyntaxRewriters;

internal sealed partial class InliningResolver
{
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
    {
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            return default;

        return base.VisitTrivia(trivia);
    }
}