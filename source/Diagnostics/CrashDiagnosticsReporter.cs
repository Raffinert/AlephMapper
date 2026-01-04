using System;
using Microsoft.CodeAnalysis;

namespace AlephMapper.Diagnostics;

internal static class CrashDiagnosticsReporter
{
    private const int MaxCrashDiagnosticLength = 2_000;

    internal static void Report(
        in SourceProductionContext sourceProductionContext,
        Exception exception
    )
    {
        sourceProductionContext.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.GeneratorCrash,
                Location.None,
                FormatCrashDiagnostic(exception)
            )
        );
    }

    private static string FormatCrashDiagnostic(Exception exception)
    {
        var details = exception.ToString().Replace("\r", " ").Replace("\n", " ");

        if (details.Length <= MaxCrashDiagnosticLength)
        {
            return details;
        }

        return $"{details.Substring(MaxCrashDiagnosticLength)}...";
    }
}
