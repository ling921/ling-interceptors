namespace Ling.Interceptors;

/// <summary>
/// Represents a safe, structured monitored value.
/// </summary>
/// <remarks>
/// Initializes a monitor value.
/// </remarks>
public sealed class MonitorValue(object? value, bool isRedacted = false)
{
    /// <summary>
    /// Gets the safe value passed to sinks.
    /// </summary>
    public object? Value { get; } = value;

    /// <summary>
    /// Gets whether the value was redacted.
    /// </summary>
    public bool IsRedacted { get; } = isRedacted;
}
