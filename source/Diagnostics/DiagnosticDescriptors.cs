using Microsoft.CodeAnalysis;

namespace AlephMapper.Diagnostics;

// Borrowed from: https://github.com/themidnightgospel/Imposter

public static class DiagnosticDescriptors
{
    private const string CrashIssueUrl =
        "https://github.com/Raffinert/AlephMapper/issues/new?labels=bug&title=Generator%20crash:%20IMP005";

    public static readonly DiagnosticDescriptor GeneratorCrash = new(
        "AM0004",
        "Generator crash",
        "Unhandled exception while generating mapping companions: '{0}'",
        "AlephMapper",
        DiagnosticSeverity.Error,
        true,
        description: "An unexpected exception bubbled out of the source generator.",
        helpLinkUri: CrashIssueUrl
    );
}
