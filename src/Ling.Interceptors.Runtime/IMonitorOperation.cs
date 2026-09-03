namespace Ling.Interceptors;

/// <summary>
/// Represents an in-flight monitored operation.
/// </summary>
public interface IMonitorOperation : IDisposable
{
    /// <summary>
    /// Records a successful completion.
    /// </summary>
    void Complete(in MonitorCompletionContext context, MonitorValue? returnValue);

    /// <summary>
    /// Records a failed completion.
    /// </summary>
    void Fail(in MonitorCompletionContext context, Exception exception);
}

internal sealed class NullMonitorOperation : IMonitorOperation
{
    internal static NullMonitorOperation Instance { get; } = new NullMonitorOperation();

    public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
    {
    }

    public void Fail(in MonitorCompletionContext context, Exception exception)
    {
    }

    public void Dispose()
    {
    }
}
