using Microsoft.CodeAnalysis;

namespace AlephMapper.Helpers;

/// <summary>
/// The effective nullable policy for one generated mapping member.
/// </summary>
internal readonly struct NullablePolicy
{
    private NullablePolicy(NullableContext context)
    {
        Context = context;
    }

    public NullableContext Context { get; }

    public bool AnnotationsEnabled => Context is
        NullableContext.Enabled or NullableContext.AnnotationsEnabled;

    public bool WarningsEnabled => Context is
        NullableContext.Enabled or NullableContext.WarningsEnabled;

    public string Directive => Context switch
    {
        NullableContext.Enabled => "enable",
        NullableContext.WarningsEnabled => "enable warnings",
        NullableContext.AnnotationsEnabled => "enable annotations",
        _ => "disable"
    };

    public static NullablePolicy Disabled { get; } = new(NullableContext.Disabled);

    public static NullablePolicy From(SemanticModel model, int position)
    {
        var context = model.GetNullableContext(position);
        var projectOptions = model.Compilation.Options.NullableContextOptions;
        var projectWarningsEnabled = projectOptions is
            NullableContextOptions.Enable or NullableContextOptions.Warnings;
        var projectAnnotationsEnabled = projectOptions is
            NullableContextOptions.Enable or NullableContextOptions.Annotations;
        var warningsEnabled = (context & NullableContext.WarningsEnabled) != 0 ||
            ((context & NullableContext.WarningsContextInherited) != 0 && projectWarningsEnabled);
        var annotationsEnabled = (context & NullableContext.AnnotationsEnabled) != 0 ||
            ((context & NullableContext.AnnotationsContextInherited) != 0 && projectAnnotationsEnabled);
        var effectiveContext = (warningsEnabled, annotationsEnabled) switch
        {
            (true, true) => NullableContext.Enabled,
            (true, false) => NullableContext.WarningsEnabled,
            (false, true) => NullableContext.AnnotationsEnabled,
            _ => NullableContext.Disabled
        };

        return new NullablePolicy(effectiveContext);
    }
}
