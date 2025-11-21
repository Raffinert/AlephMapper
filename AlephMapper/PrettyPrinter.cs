using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

public sealed class PrettyPrinter : CSharpSyntaxVisitor
{
    private readonly StringBuilder _sb = new();
    private int _indent;
    private bool _atLineStart = false;

    private PrettyPrinter(int baseIndent)
    {
        _indent = baseIndent;
    }

    public static string Print(SyntaxNode node, int baseIndent = 0)
    {
        var f = new PrettyPrinter(baseIndent);
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
        // Avoid stacking multiple blank lines when we're already at the start of a line
        if (_sb.Length > 0)
        {
            var last = _sb[_sb.Length - 1];
            if (last == '\n' || last == '\r')
            {
                _atLineStart = true;
                return;
            }
        }

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

        _sb.Append(text);

        _atLineStart = token.TrailingTrivia.Any(tr =>
            tr == SyntaxFactory.CarriageReturn ||
            tr == SyntaxFactory.LineFeed ||
            tr == SyntaxFactory.CarriageReturnLineFeed);
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

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Visit(node.Condition.WithoutTrailingTrivia());
        WriteLine();
        Indent();
        WriteRaw("? ");
        Visit(node.WhenTrue.WithoutLeadingTrivia());
        WriteLine();
        WriteRaw(": ");
        Visit(node.WhenFalse.WithoutLeadingTrivia());
        Unindent();
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        Visit(node.Left.WithoutTrailingTrivia());
        WriteRaw(" ");
        WriteRaw(node.OperatorToken.Text);
        WriteRaw(" ");
        Visit(node.Right.WithoutLeadingTrivia());
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
