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

    public static readonly DiagnosticDescriptor InvalidAdaptType = new(
        "AM0005",
        "Invalid adapted source or destination type",
        "Adaptation for method '{0}' has an invalid source or destination type.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptSourceMemberMissing = new(
        "AM0006",
        "Required adapted source member is missing",
        "Cannot adapt '{0}': source member path '{1}' cannot be resolved on '{2}'.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptDestinationMemberMissing = new(
        "AM0007",
        "Required adapted destination member is missing or not writable",
        "Cannot adapt '{0}': destination member '{1}' is missing or not writable on '{2}'.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptIncompatibleType = new(
        "AM0008",
        "Adapted expression or assignment has an incompatible type",
        "Cannot adapt '{0}': adapted expression or assignment is not type-compatible for '{1}'.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptNameConflict = new(
        "AM0009",
        "Generated adapted method name or signature conflicts",
        "Cannot adapt '{0}': generated member name or signature '{1}' conflicts with an existing or generated member.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptUnsupportedSyntax = new(
        "AM0010",
        "Template contains unsupported adaptation syntax",
        "Cannot adapt '{0}': the template contains unsupported syntax '{1}'.",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptExpressionWithoutName = new(
        "AM0011",
        "Expression generation requested without a generated name",
        "Cannot adapt '{0}': Name is required when Generate includes Expression.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptDuplicatePair = new(
        "AM0012",
        "Duplicate adaptation for the same explicit type pair",
        "Cannot adapt '{0}': duplicate adaptation for source '{1}' and destination '{2}'.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptCircularHelper = new(
        "AM0013",
        "Circular helper reference prevents adaptation",
        "Adaptation skipped for '{0}' due to circular helper references.",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptOpenGenericType = new(
        "AM0014",
        "Adapted source or destination type is open generic",
        "Cannot adapt '{0}': adapted source or destination type must not be open generic.",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AdaptRebindingFailed = new(
        "AM0015",
        "Generated adapted method fails Roslyn rebinding",
        "Cannot adapt '{0}': generated adapted member failed compilation validation: {1}",
        "AlephMapper",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsafeNullConditionalReceiver = new(
        "AM0016",
        "Null-conditional receiver cannot be safely rewritten",
        "Expression generation for '{0}' was skipped because null-conditional receiver '{1}' may be evaluated more than once",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Rewrite repeats the receiver in the null check and non-null branch. Use a stable member-access receiver to preserve C# evaluation semantics.");

    public static readonly DiagnosticDescriptor UnsupportedNullConditionalExpression = new(
        "AM0017",
        "Null-conditional access is unsupported in expression trees",
        "Expression generation for '{0}' was skipped because NullConditionalRewrite.None preserves unsupported null-conditional expression '{1}'",
        "AlephMapper",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Select NullConditionalRewrite.Ignore or NullConditionalRewrite.Rewrite to generate an expression-tree-compatible companion.");
}
