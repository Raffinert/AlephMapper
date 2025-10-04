using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper;

internal static class EmitHelpers
{
    public static bool TryBuildUpdateAssignments(ExpressionSyntax body, string destPrefix, List<string> lines)
    {
        var oce = body as ObjectCreationExpressionSyntax;
        if (oce == null) return false;
        var init = oce.Initializer;
        if (init == null || init.Expressions.Count == 0) return false;

        foreach (var expr in init.Expressions)
        {
            var assign = expr as AssignmentExpressionSyntax;
            if (assign == null) return false;

            var leftText = assign.Left.ToString();
            var rightText = assign.Right.ToString();

            lines.Add(destPrefix + "." + leftText + " = " + rightText + ";");
        }

        return lines.Count > 0;
    }
}