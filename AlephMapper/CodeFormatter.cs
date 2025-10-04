using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace AlephMapper;

/// <summary>
/// Provides code formatting utilities for generated C# code
/// </summary>
internal static class CodeFormatter
{
    /// <summary>
    /// Formats generated code using Roslyn's CompilationUnit with proper C# brace formatting
    /// </summary>
    /// <param name="sourceCode">The source code to format</param>
    /// <returns>Formatted source code</returns>
    public static string FormatGeneratedCode(string sourceCode)
    {
        try
        {
            // Parse the source code into a CompilationUnit
            var compilationUnit = SyntaxFactory.ParseCompilationUnit(sourceCode);
            
            // Check if there are any parsing errors and handle them gracefully
            if (compilationUnit.ContainsDiagnostics)
            {
                var errors = compilationUnit.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
                if (errors.Any())
                {
                    // If there are errors, return the original code to avoid breaking generation
                    return sourceCode;
                }
            }
            
            // Normalize whitespace for consistent formatting
            var formatted = compilationUnit.NormalizeWhitespace(
                indentation: "    ", 
                elasticTrivia: false 
            );
            
            return formatted.ToFullString().Replace("\r\n\r\n", "\r\n").Replace("\n\n", "\n");
        }
        catch
        {
            // If formatting fails for any reason, return the original code
            // This ensures the generator doesn't break due to formatting issues
            return sourceCode;
        }
    }
}