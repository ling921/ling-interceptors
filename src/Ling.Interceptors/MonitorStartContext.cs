namespace Ling.Interceptors;

/// <summary>
/// Contains immutable metadata for a monitored method invocation.
/// </summary>
/// <remarks>
/// Initializes a start context.
/// </remarks>
public readonly struct MonitorStartContext(
    string category,
    string methodName,
    IReadOnlyDictionary<string, MonitorValue> parameters,
    bool createTrace)
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
    /// Gets the already-formatted parameter values.
    /// </summary>
    public IReadOnlyDictionary<string, MonitorValue> Parameters { get; } = parameters;

    /// <summary>
    /// Gets whether the operation requests a tracing activity.
    /// </summary>
    public bool CreateTrace { get; } = createTrace;
}
