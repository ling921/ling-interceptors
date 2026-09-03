namespace Ling.Interceptors;

/// <summary>
/// Describes a value being formatted for monitoring.
/// </summary>
/// <remarks>
/// Initializes a value context.
/// </remarks>
public readonly struct MonitorValueContext(string name, Type declaredType, bool isSensitive)
{
    /// <summary>
    /// Gets the parameter or return-value name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the declared value type.
    /// </summary>
    public Type DeclaredType { get; } = declaredType;

    /// <summary>
    /// Gets whether the raw value must not be exposed.
    /// </summary>
    public bool IsSensitive { get; } = isSensitive;
}
