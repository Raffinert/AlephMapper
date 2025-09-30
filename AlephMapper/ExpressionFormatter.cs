using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AlephMapper
{
    internal static class ExpressionFormatter
    {
        public static string FormatExpression(ExpressionSyntax expressionSyntax, string baseIndent)
        {
            var expression = expressionSyntax.ToString();
            if (!expression.Contains("new ") || !expression.Contains("{"))
                return expression;

            return FormatExpressionRecursively(expression, baseIndent);
        }

        // Keep the old method for backward compatibility during transition
        public static string FormatExpression(string expression, string baseIndent)
        {
            if (!expression.Contains("new ") || !expression.Contains("{"))
                return expression;

            return FormatExpressionRecursively(expression, baseIndent);
        }

        private static string FormatExpressionRecursively(string expression, string baseIndent)
        {
            var trimmed = expression.Trim();
            var lambdaIndex = trimmed.IndexOf(" => ", StringComparison.Ordinal);
            if (lambdaIndex > 0)
            {
                var parameter = trimmed.Substring(0, lambdaIndex);
                var body = trimmed.Substring(lambdaIndex + 4);
                var formattedBody = FormatObjectCreation(body.Trim(), baseIndent);
                return $"{parameter} => {formattedBody}";
            }
            return FormatObjectCreation(trimmed, baseIndent);
        }

        private static string FormatObjectCreation(string expression, string baseIndent)
        {
            var trimmed = expression.Trim();
            if (trimmed.StartsWith("new "))
                return FormatNewExpression(trimmed, baseIndent);
            var questionIndex = FindConditionalOperator(trimmed);
            if (questionIndex > 0)
                return FormatConditionalExpression(trimmed, questionIndex, baseIndent);
            return trimmed;
        }

        private static string FormatNewExpression(string expression, string baseIndent)
        {
            var openBraceIndex = expression.IndexOf('{');
            if (openBraceIndex < 0)
                return expression;
            var closeBraceIndex = FindMatchingBrace(expression, openBraceIndex);
            if (closeBraceIndex < 0)
                return expression;
            var typeDeclaration = expression.Substring(0, openBraceIndex).Trim();
            var propertiesContent = expression.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();
            if (string.IsNullOrEmpty(propertiesContent))
                return $"{typeDeclaration}()";
            var properties = ParsePropertiesForNewExpression(propertiesContent, baseIndent);
            return $"{typeDeclaration}\r\n{baseIndent}{{\r\n{string.Join(",\r\n", properties)}\r\n{baseIndent}}}";
        }

        private static List<string> ParsePropertiesForNewExpression(string propertiesContent, string baseIndent)
        {
            var properties = new List<string>();
            var current = new StringBuilder();
            var braceLevel = 0;
            var inString = false;
            var escapeNext = false;
            for (int i = 0; i < propertiesContent.Length; i++)
            {
                var ch = propertiesContent[i];
                if (escapeNext)
                {
                    current.Append(ch);
                    escapeNext = false;
                    continue;
                }
                if (ch == '\\' && inString)
                {
                    current.Append(ch);
                    escapeNext = true;
                    continue;
                }
                if (ch == '"')
                {
                    inString = !inString;
                    current.Append(ch);
                    continue;
                }
                if (inString)
                {
                    current.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '{':
                        braceLevel++;
                        current.Append(ch);
                        break;
                    case '}':
                        braceLevel--;
                        current.Append(ch);
                        break;
                    case ',':
                        if (braceLevel == 0)
                        {
                            var propertyValue = current.ToString().Trim();
                            var formattedProperty = FormatPropertyAssignment(propertyValue, baseIndent);
                            properties.Add($"{baseIndent}    {formattedProperty}");
                            current.Clear();
                        }
                        else
                        {
                            current.Append(ch);
                        }
                        break;
                    default:
                        current.Append(ch);
                        break;
                }
            }
            if (current.Length > 0)
            {
                var propertyValue = current.ToString().Trim();
                var formattedProperty = FormatPropertyAssignment(propertyValue, baseIndent);
                properties.Add($"{baseIndent}    {formattedProperty}");
            }
            return properties;
        }

        private static string FormatPropertyAssignment(string propertyAssignment, string baseIndent)
        {
            var equalIndex = propertyAssignment.IndexOf('=');
            if (equalIndex <= 0)
                return propertyAssignment;
            var propertyName = propertyAssignment.Substring(0, equalIndex).Trim();
            var propertyValue = propertyAssignment.Substring(equalIndex + 1).Trim();
            var questionIndex = FindConditionalOperator(propertyValue);
            if (questionIndex > 0)
            {
                var formattedValue = FormatConditionalExpression(propertyValue, questionIndex, baseIndent);
                if (formattedValue.Contains("\r\n"))
                    return $"{propertyName} = {formattedValue}";
            }
            return propertyAssignment;
        }

        private static string FormatConditionalExpression(string expression, int questionIndex, string baseIndent)
        {
            var condition = expression.Substring(0, questionIndex).Trim();
            var remaining = expression.Substring(questionIndex + 1).Trim();
            var colonIndex = FindConditionalColon(remaining);
            if (colonIndex < 0)
                return expression;
            var whenTrue = remaining.Substring(0, colonIndex).Trim();
            var whenFalse = remaining.Substring(colonIndex + 1).Trim();
            if (whenTrue.Contains("new ") && whenTrue.Contains("{"))
            {
                var openBraceIndex = whenTrue.IndexOf('{');
                if (openBraceIndex > 0)
                {
                    var closeBraceIndex = FindMatchingBrace(whenTrue, openBraceIndex);
                    if (closeBraceIndex > 0)
                    {
                        var typeDeclaration = whenTrue.Substring(0, openBraceIndex).Trim();
                        var propertiesContent = whenTrue.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();
                        if (!string.IsNullOrEmpty(propertiesContent))
                        {
                            var properties = ParsePropertiesSimple(propertiesContent);
                            var formattedProps = string.Join($",\r\n{baseIndent}            ", properties);
                            var formattedObject = $"{typeDeclaration}\r\n{baseIndent}        {{\r\n{baseIndent}            {formattedProps}\r\n{baseIndent}        }}";
                            return $"{condition} ?\r\n{baseIndent}        {formattedObject} :\r\n{baseIndent}        {whenFalse}";
                        }
                    }
                }
            }
            return $"{condition} ? {whenTrue} : {whenFalse}";
        }

        private static List<string> ParsePropertiesSimple(string propertiesContent)
        {
            var properties = new List<string>();
            var parts = propertiesContent.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    properties.Add(trimmed);
            }
            return properties;
        }

        public static int FindConditionalOperator(string expression)
        {
            var braceLevel = 0;
            var inString = false;
            var escapeNext = false;
            for (int i = 0; i < expression.Length; i++)
            {
                var ch = expression[i];
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }
                if (ch == '\\' && inString)
                {
                    escapeNext = true;
                    continue;
                }
                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                    continue;
                switch (ch)
                {
                    case '{':
                        braceLevel++;
                        break;
                    case '}':
                        braceLevel--;
                        break;
                    case '?':
                        if (braceLevel == 0)
                            return i;
                        break;
                }
            }
            return -1;
        }

        public static int FindConditionalColon(string expression)
        {
            var braceLevel = 0;
            var inString = false;
            var escapeNext = false;
            for (int i = 0; i < expression.Length; i++)
            {
                var ch = expression[i];
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }
                if (ch == '\\' && inString)
                {
                    escapeNext = true;
                    continue;
                }
                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                    continue;
                switch (ch)
                {
                    case '{':
                        braceLevel++;
                        break;
                    case '}':
                        braceLevel--;
                        break;
                    case ':':
                        if (braceLevel == 0)
                            return i;
                        break;
                }
            }
            return -1;
        }

        public static int FindMatchingBrace(string expression, int openBraceIndex)
        {
            var braceLevel = 1;
            var inString = false;
            var escapeNext = false;
            for (int i = openBraceIndex + 1; i < expression.Length; i++)
            {
                var ch = expression[i];
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }
                if (ch == '\\' && inString)
                {
                    escapeNext = true;
                    continue;
                }
                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                    continue;
                switch (ch)
                {
                    case '{':
                        braceLevel++;
                        break;
                    case '}':
                        braceLevel--;
                        if (braceLevel == 0)
                            return i;
                        break;
                }
            }
            return -1;
        }
    }
}
