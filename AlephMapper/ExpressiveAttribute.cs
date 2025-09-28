using System;

namespace AlephMapper;

/// <summary>
/// Configures how null-conditional operators are handled
/// </summary>
public enum NullConditionalRewriteSupport
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
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ExpressiveAttribute : Attribute
{
    /// <summary>
    /// Get or set how null-conditional operators are handled
    /// </summary>
    public NullConditionalRewriteSupport NullConditionalRewriteSupport { get; set; } = NullConditionalRewriteSupport.Ignore;
}