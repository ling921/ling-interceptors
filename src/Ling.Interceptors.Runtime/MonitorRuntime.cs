namespace Ling.Interceptors;

/// <summary>
/// Provides the process-wide monitoring configuration used by generated code.
/// </summary>
public static class MonitorRuntime
{
    private static IMonitorSink s_sink = NullMonitorSink.Instance;
    private static IMonitorValueFormatter s_formatter = DefaultMonitorValueFormatter.Instance;

    /// <summary>
    /// Gets or sets the configured sink. Defaults to a no-op sink.
    /// </summary>
    public static IMonitorSink Sink
    {
        get => Volatile.Read(ref s_sink);
        set => Volatile.Write(ref s_sink, value ?? NullMonitorSink.Instance);
    }

    /// <summary>
    /// Gets or sets the configured formatter.
    /// </summary>
    public static IMonitorValueFormatter Formatter
    {
        get => Volatile.Read(ref s_formatter);
        set => Volatile.Write(ref s_formatter, value ?? DefaultMonitorValueFormatter.Instance);
    }

    /// <summary>
    /// Formats a value without allowing formatter failures to affect application code.
    /// </summary>
    public static MonitorValue Format(object? value, in MonitorValueContext context)
    {
        try
        {
            return Formatter.Format(value, context) ?? new MonitorValue("[UNAVAILABLE]");
        }
        catch
        {
            return new MonitorValue(context.IsSensitive ? "[REDACTED]" : "[UNAVAILABLE]", context.IsSensitive);
        }
    }

    /// <summary>
    /// Starts a sink operation without allowing sink failures to affect application code.
    /// </summary>
    public static IMonitorOperation Begin(in MonitorStartContext context)
    {
        try
        {
            return new SafeMonitorOperation(Sink.Begin(context) ?? NullMonitorOperation.Instance);
        }
        catch
        {
            return NullMonitorOperation.Instance;
        }
    }

    private sealed class SafeMonitorOperation(IMonitorOperation inner) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            try
            {
                inner.Complete(context, returnValue);
            }
            catch { }
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            try
            {
                inner.Fail(context, exception);
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                inner.Dispose();
            }
            catch { }
        }
    }
}
