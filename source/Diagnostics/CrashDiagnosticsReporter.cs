using System;
using AlephMapper.Generation;
using Microsoft.CodeAnalysis;

namespace AlephMapper.Diagnostics;

// Borrowed from: https://github.com/themidnightgospel/Imposter

internal static class CrashDiagnosticsReporter
{
    private const int MaxCrashDiagnosticLength = 2_000;

    internal static GenerationDiagnostic CreateDiagnostic(Exception exception)
    {
        return Generation.GenerationDiagnostic.From(Diagnostic.Create(
            DiagnosticDescriptors.GeneratorCrash,
            Location.None,
            FormatCrashDiagnostic(exception)));
    }

    private static string FormatCrashDiagnostic(Exception exception)
    {
        var details = exception.ToString().Replace("\r", " ").Replace("\n", " ");

        if (details.Length <= MaxCrashDiagnosticLength)
        {
            return details;
        }

        return $"{details[..MaxCrashDiagnosticLength]}...";
    }
}
