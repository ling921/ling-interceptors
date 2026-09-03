namespace Ling.Interceptors;

/// <summary>
/// Defines where an interception rule applies.
/// </summary>
[Flags]
public enum InterceptionScope
{
    /// <summary>
    /// Disables the rule.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only call sites with an explicit interception marker.
    /// </summary>
    Explicit = 1,

    /// <summary>
    /// Ordinary calls in the current compilation.
    /// </summary>
    Compilation = 2,

    /// <summary>
    /// Generated-code calls in the current compilation.
    /// </summary>
    GeneratedCode = 4,
}
