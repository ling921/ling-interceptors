namespace Ling.Interceptors;

/// <summary>Requests compile-time monitoring for calls to the marked method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MonitorAttribute : Attribute
{
    public bool CaptureParameters { get; set; }
    public bool CaptureReturnValue { get; set; }
    public bool CaptureExceptions { get; set; } = true;
    public bool RecordTiming { get; set; } = true;
    public bool CreateTrace { get; set; }
    public InterceptionScope Scope { get; set; } = InterceptionScope.Compilation;
}
