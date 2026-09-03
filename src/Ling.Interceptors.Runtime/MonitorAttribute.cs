namespace Ling.Interceptors;

/// <summary>
/// Requests compile-time monitoring for calls to the marked method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MonitorAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether parameter values are captured.
    /// </summary>
    public bool CaptureParameters { get; set; }

    /// <summary>
    /// Gets or sets whether the return value is captured.
    /// </summary>
    public bool CaptureReturnValue { get; set; }

    /// <summary>
    /// Gets or sets whether exceptions are reported. The default is <see langword="true"/>.
    /// </summary>
    public bool CaptureExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether elapsed time is recorded. The default is <see langword="true"/>.
    /// </summary>
    public bool RecordTiming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a tracing activity is created.
    /// </summary>
    public bool CreateTrace { get; set; }

    /// <summary>
    /// Gets or sets the call-site scope. The default is <see cref="InterceptionScope.Compilation"/>.
    /// </summary>
    public InterceptionScope Scope { get; set; } = InterceptionScope.Compilation;
}
