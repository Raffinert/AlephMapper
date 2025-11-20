using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

public sealed class ObjectCreationFormatter : CSharpSyntaxVisitor
{
    private readonly StringBuilder _sb = new();
    private int _indent;
    private bool _atLineStart = false;

    private ObjectCreationFormatter(int baseIndent)
    {
        _indent = baseIndent;
    }

    public static string Format(SyntaxNode node, int baseIndent = 0)
    {
        var f = new ObjectCreationFormatter(baseIndent);
        f.Visit(node);
        return f._sb.ToString();
    }

    // -------- helpers --------

    private void WriteRaw(string text)
    {
        if (_atLineStart)
        {
            _sb.Append(new string(' ', _indent * 4));
            _atLineStart = false;
        }
        _sb.Append(text);
    }

    private void WriteLine()
    {
        _sb.AppendLine();
        _atLineStart = true;
    }

    private void TrimTrailingWhitespace(int startIndex)
    {
        var length = _sb.Length;

        while (length > startIndex)
        {
            var ch = _sb[length - 1];

            if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t')
            {
                length--;
                continue;
            }

            break;
        }

        if (length != _sb.Length)
        {
            _sb.Length = length;
            _atLineStart = false;
        }
    }

    private void Indent() => _indent++;
    private void Unindent() { if (_indent > 0) _indent--; }

    private void AppendToken(SyntaxToken token)
    {
        var text = token.ToFullString();
        if (text.Length == 0)
            return;
        
        //text = text.Replace("\r\n", new string(' ', _indent * 4))
        //    .Replace("\n", new string(' ', _indent * 4))
        //    .Replace("\r", new string(' ', _indent * 4));
        //_atLineStart = token.LeadingTrivia.Any(tr =>
        //    tr == SyntaxFactory.CarriageReturn || tr == SyntaxFactory.LineFeed ||
        //    tr == SyntaxFactory.CarriageReturnLineFeed);

        _sb.Append(text);

        _atLineStart = token.TrailingTrivia.Any(tr =>
            tr == SyntaxFactory.CarriageReturn || 
            tr == SyntaxFactory.LineFeed ||
            tr == SyntaxFactory.CarriageReturnLineFeed);

        //var last = text.Last();
        //_atLineStart = last == '\n' || last == '\r';
    }

    // -------- default: walk children & keep original formatting --------

    public override void DefaultVisit(SyntaxNode node)
    {
        // Important: walk children, so nested `new` calls still go through
        // VisitObjectCreationExpression, but keep *token* text as-is.
        foreach (var child in node.ChildNodesAndTokens())
        {
            if (child.IsToken)
            {
                AppendToken(child.AsToken());
            }
            else
            {
                Visit(child.AsNode()!); // may hit our ObjectCreation override
            }
        }
    }

    // -------- the only special case: object creation --------

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // We ignore the original trivia around "new" and print it cleanly.
        // Surrounding code stays as-is because DefaultVisit preserves tokens.
        WriteRaw("new ");
        // Type (no trivia needed here)
        _sb.Append(node.Type);

        // Arguments (keep original formatting)
        if (node.ArgumentList is { } args)
            Visit(args);

        if (node.Initializer is null)
            return;

        WriteLine();
        WriteRaw("{");
        Indent();

        var exprs = node.Initializer.Expressions;
        for (int i = 0; i < exprs.Count; i++)
        {
            WriteLine();
            WriteRaw(string.Empty); // ensure correct indentation for each entry
            var entryStart = _sb.Length;
            Visit(exprs[i].WithoutLeadingTrivia()); // this can contain nested `new ... { ... }`
            TrimTrailingWhitespace(entryStart);

            if (i < exprs.Count - 1)
                _sb.Append(",");
        }

        Unindent();
        WriteLine();
        WriteRaw("}");
    }
}
