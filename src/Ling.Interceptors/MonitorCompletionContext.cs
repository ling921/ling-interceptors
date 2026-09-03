namespace Ling.Interceptors;

/// <summary>
/// Contains immutable completion metadata for a monitored method invocation.
/// </summary>
/// <remarks>
/// Initializes a completion context.
/// </remarks>
public readonly struct MonitorCompletionContext(
    string category,
    string methodName,
    TimeSpan duration,
    bool recordTiming)
{
    /// <summary>
    /// Gets the logger or instrumentation category.
    /// </summary>
    public string Category { get; } = category;

    /// <summary>
    /// Gets the method name.
    /// </summary>
    public string MethodName { get; } = methodName;

    /// <summary>
    /// Gets the elapsed invocation duration.
    /// </summary>
    public TimeSpan Duration { get; } = duration;

    /// <summary>
    /// Gets whether duration should be recorded as a metric.
    /// </summary>
    public bool RecordTiming { get; } = recordTiming;
}
