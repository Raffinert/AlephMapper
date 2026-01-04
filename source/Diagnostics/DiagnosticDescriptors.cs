using Microsoft.CodeAnalysis;

namespace AlephMapper.Diagnostics;

// Borrowed from: https://github.com/themidnightgospel/Imposter

public static class DiagnosticDescriptors
{
    private const string CrashIssueUrl =
        "https://github.com/Raffinert/AlephMapper/issues/new?labels=bug&title=Generator%20crash:%20IMP005";

    public static readonly DiagnosticDescriptor UpdatableValueTypeReturn = new(
        "AM0001",
        "Updatable method with value type return type",
        "Updatable method '{0}' returns value type '{1}'. Value types are passed by value, so update semantics don't work as expected. Consider using a regular mapping method instead.",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ExpressiveCircularReferences = new(
        "AM0002",
        "Expressive method generation skipped due to circular references",
        "Expression method generation skipped for '{0}' due to circular references. Fix the circular dependencies to enable expression generation.",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UpdatableCircularReferences = new(
        "AM0003",
        "Updatable method generation skipped due to circular references",
        "Updatable method generation skipped for '{0}' due to circular references. Fix the circular dependencies to enable Updatable method generation.",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

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
