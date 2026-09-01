using System.Collections.Generic;
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
        TrimTrailingWhitespace(0, includeLineBreaks: false);

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

    private void TrimTrailingWhitespace(int startIndex, bool includeLineBreaks = true)
    {
        var length = _sb.Length;

        while (length > startIndex)
        {
            var ch = _sb[length - 1];

            if (ch == ' ' || ch == '\t' || (includeLineBreaks && (ch == '\r' || ch == '\n')))
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

        WriteRaw(text);

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
        if (!HasMultilineConditionalLayout(node))
        {
            Visit(node.Condition.WithoutTrailingTrivia());
            WriteRaw(" ? ");
            Visit(node.WhenTrue.WithoutLeadingTrivia().WithoutTrailingTrivia());
            WriteRaw(" : ");
            Visit(node.WhenFalse.WithoutLeadingTrivia());
            return;
        }

        Visit(node.Condition.WithoutTrailingTrivia());
        WriteLine();
        Indent();
        WriteRaw("? ");
        Visit(node.WhenTrue.WithoutLeadingTrivia().WithoutTrailingTrivia());
        WriteLine();
        WriteRaw(": ");
        Visit(node.WhenFalse.WithoutLeadingTrivia());
        Unindent();
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (TryCollectLogicalChain(node, out var operands, out var operatorText))
        {
            Visit(operands[0].WithoutTrailingTrivia());
            Indent();

            for (var i = 1; i < operands.Count; i++)
            {
                WriteLine();
                WriteRaw(operatorText);
                WriteRaw(" ");
                Visit(operands[i].WithoutLeadingTrivia());
            }

            Unindent();
            return;
        }

        Visit(node.Left.WithoutTrailingTrivia());
        WriteRaw(" ");
        WriteRaw(node.OperatorToken.Text);
        WriteRaw(" ");
        Visit(node.Right.WithoutLeadingTrivia());
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (!TryCollectFluentChain(node, out var root, out var calls))
        {
            base.VisitInvocationExpression(node);
            return;
        }

        Visit(root.WithoutTrailingTrivia());
        Indent();

        foreach (var (name, arguments) in calls)
        {
            WriteLine();
            WriteRaw(".");
            Visit(name.WithoutTrivia());
            Visit(arguments.WithoutTrivia());
        }

        Unindent();
    }

    public override void VisitArgumentList(ArgumentListSyntax node)
    {
        if (node.Arguments.Count <= 1 || !ContainsLineBreak(node.ToFullString()))
        {
            base.VisitArgumentList(node);
            return;
        }

        WriteRaw("(");
        Indent();

        for (var i = 0; i < node.Arguments.Count; i++)
        {
            WriteLine();
            Visit(node.Arguments[i].WithoutTrivia());

            if (i < node.Arguments.Count - 1)
            {
                WriteRaw(",");
            }
        }

        Unindent();
        WriteLine();
        WriteRaw(")");
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

    public override void VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
    {
        WriteRaw("new");
        WriteLine();
        WriteRaw("{");
        Indent();

        var initializers = node.Initializers;
        for (int i = 0; i < initializers.Count; i++)
        {
            WriteLine();
            WriteRaw(string.Empty);
            var entryStart = _sb.Length;
            VisitAnonymousObjectMemberDeclarator(initializers[i]);
            TrimTrailingWhitespace(entryStart);

            if (i < initializers.Count - 1)
                _sb.Append(",");
        }

        Unindent();
        WriteLine();
        WriteRaw("}");
    }

    public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
    {
        if (node.NameEquals != null)
        {
            Visit(node.NameEquals.Name.WithoutTrivia());
            WriteRaw(" = ");
        }

        Visit(node.Expression.WithoutLeadingTrivia());
    }

    private static bool TryCollectFluentChain(
        InvocationExpressionSyntax node,
        out ExpressionSyntax root,
        out IReadOnlyList<(SimpleNameSyntax Name, ArgumentListSyntax Arguments)> calls)
    {
        var collectedCalls = new List<(SimpleNameSyntax Name, ArgumentListSyntax Arguments)>();
        ExpressionSyntax current = node;

        while (current is InvocationExpressionSyntax invocation &&
               invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.OperatorToken.IsKind(SyntaxKind.DotToken))
        {
            collectedCalls.Add((memberAccess.Name, invocation.ArgumentList));
            current = memberAccess.Expression;
        }

        collectedCalls.Reverse();
        root = current;
        calls = collectedCalls;
        return collectedCalls.Count > 1;
    }

    private static bool TryCollectLogicalChain(
        BinaryExpressionSyntax node,
        out IReadOnlyList<ExpressionSyntax> operands,
        out string operatorText)
    {
        operands = [];
        operatorText = node.OperatorToken.Text;
        if (!IsLogicalChainOperator(node.Kind()) || !HasLineBreakInLogicalChain(node, node.Kind()))
        {
            return false;
        }

        var collectedOperands = new List<ExpressionSyntax>();
        CollectLogicalOperands(node, node.Kind(), collectedOperands);
        operands = collectedOperands;
        return collectedOperands.Count > 1;
    }

    private static void CollectLogicalOperands(
        ExpressionSyntax expression,
        SyntaxKind chainKind,
        List<ExpressionSyntax> operands)
    {
        if (expression is BinaryExpressionSyntax binaryExpression && binaryExpression.IsKind(chainKind))
        {
            CollectLogicalOperands(binaryExpression.Left, chainKind, operands);
            CollectLogicalOperands(binaryExpression.Right, chainKind, operands);
            return;
        }

        operands.Add(expression);
    }

    private static bool IsLogicalChainOperator(SyntaxKind kind)
    {
        return kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;
    }

    private static bool HasLineBreakInLogicalChain(ExpressionSyntax expression, SyntaxKind chainKind)
    {
        if (expression is not BinaryExpressionSyntax binaryExpression || !binaryExpression.IsKind(chainKind))
        {
            return false;
        }

        return ContainsLineBreak(binaryExpression.Left.GetTrailingTrivia()) ||
               ContainsLineBreak(binaryExpression.OperatorToken.LeadingTrivia) ||
               ContainsLineBreak(binaryExpression.OperatorToken.TrailingTrivia) ||
               ContainsLineBreak(binaryExpression.Right.GetLeadingTrivia()) ||
               HasLineBreakInLogicalChain(binaryExpression.Left, chainKind) ||
               HasLineBreakInLogicalChain(binaryExpression.Right, chainKind);
    }

    private static bool HasMultilineConditionalLayout(ConditionalExpressionSyntax node)
    {
        if (node.GetAnnotations(GeneratedSyntaxAnnotations.MultilineConditional).Any())
        {
            return true;
        }

        if (node.GetAnnotations(GeneratedSyntaxAnnotations.SingleLineConditional).Any())
        {
            return ContainsLineBreak(node.WhenTrue.ToFullString()) ||
                   ContainsLineBreak(node.WhenFalse.ToFullString());
        }

        // Rewriters synthesize conditionals for null-conditional access. Keep their
        // established, readable multi-line form unless the source layout was recorded.
        return true;
    }

    private static bool ContainsLineBreak(SyntaxTriviaList trivia)
    {
        return trivia.Any(triviaItem => triviaItem.IsKind(SyntaxKind.EndOfLineTrivia));
    }

    private static bool ContainsLineBreak(string text)
    {
        return text.Contains('\n') || text.Contains('\r');
    }
}
