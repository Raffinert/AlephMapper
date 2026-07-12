using System;

namespace AlephMapper;

/// <summary>
/// Configures how null-conditional operators are handled
/// </summary>
public enum NullConditionalRewrite
{
    /// <summary>
    /// Don't rewrite null conditional operators (Default behavior).
    /// Usage of null conditional operators is thereby not allowed
    /// </summary>
    None,

    /// <summary>
    /// Ignore null-conditional operators in the generated expression tree
    /// </summary>
    /// <remarks>
    /// <c>(A?.B)</c> is rewritten as expression: <c>(A.B)</c>
    /// </remarks>
    Ignore,

    /// <summary>
    /// Translates null-conditional operators into explicit null checks
    /// </summary>
    /// <remarks>
    /// <c>(A?.B)</c> is rewritten as expression: <c>(A != null ? A.B : null)</c>
    /// </remarks>
    Rewrite
}

/// <summary>
/// Marks a class to generate expressive companion methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExpressiveAttribute : Attribute
{
    /// <summary>
    /// Get or set how null-conditional operators are handled
    /// </summary>
    public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
}

/// <summary>
/// Marks a class to generate update companion methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class UpdatableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the policy for handling collection updates during mapping operations
    /// </summary>
    public CollectionPropertiesPolicy CollectionProperties { get; set; } = CollectionPropertiesPolicy.Skip;
}

/// <summary>
/// Defines which adapted companions are generated for an <see cref="AdaptAttribute"/> declaration.
/// </summary>
[Flags]
public enum AdaptGeneration
{
    /// <summary>
    /// Generate a regular mapping method.
    /// </summary>
    Map = 1,

    /// <summary>
    /// Generate an expression companion method.
    /// </summary>
    Expression = 2,

    /// <summary>
    /// Generate an overload that updates an existing destination instance.
    /// </summary>
    Update = 4
}

/// <summary>
/// Reuses a mapping method as a compile-time template for one explicitly specified source/destination pair.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AdaptAttribute : Attribute
{
    /// <summary>
    /// Initializes a new adaptation from the template method to the specified source and destination types.
    /// </summary>
    public AdaptAttribute(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }

    /// <summary>
    /// Gets the adapted source type.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the adapted destination type.
    /// </summary>
    public Type DestinationType { get; }

    /// <summary>
    /// Gets or sets the generated method base name.
    /// Required when expression generation is requested.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets which adapted companions are generated.
    /// </summary>
    public AdaptGeneration Generate { get; set; } = AdaptGeneration.Map | AdaptGeneration.Expression;

    /// <summary>
    /// Gets or sets how null-conditional operators are handled.
    /// </summary>
    public NullConditionalRewrite NullConditionalRewrite { get; set; } = NullConditionalRewrite.Ignore;
}

/// <summary>
/// Defines the policy for handling collection updates during mapping operations
/// </summary>
public enum CollectionPropertiesPolicy
{
    /// <summary>
    /// Skip collection updates - collections will not be modified during mapping
    /// </summary>
    Skip,

    /// <summary>
    /// Update collections - collections will be updated during mapping operations
    /// </summary>
    Update
}
