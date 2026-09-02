using Microsoft.CodeAnalysis;

namespace AlephMapper.Helpers;

/// <summary>
/// The project-level nullable policy for generated code.
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

    public static NullablePolicy Disabled { get; } = new(NullableContext.Disabled);

    public static NullablePolicy From(Compilation compilation)
        => From(compilation.Options.NullableContextOptions);

    public static NullablePolicy From(NullableContextOptions options)
    {
        var context = options switch
        {
            NullableContextOptions.Enable => NullableContext.Enabled,
            NullableContextOptions.Warnings => NullableContext.WarningsEnabled,
            NullableContextOptions.Annotations => NullableContext.AnnotationsEnabled,
            _ => NullableContext.Disabled
        };

        return new NullablePolicy(context);
    }
}
