namespace Ling.Interceptors;

/// <summary>
/// Receives lifecycle notifications for a monitored method invocation.
/// </summary>
public interface IMonitorSink
{
    /// <summary>
    /// Starts a monitored operation.
    /// </summary>
    IMonitorOperation Begin(in MonitorStartContext context);
}

/// <summary>
/// A no-op monitor sink used until an application configures monitoring.
/// </summary>
public sealed class NullMonitorSink : IMonitorSink
{
    /// <summary>
    /// Gets the shared sink instance.
    /// </summary>
    public static NullMonitorSink Instance { get; } = new NullMonitorSink();

    private NullMonitorSink()
    {
    }

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context) => NullMonitorOperation.Instance;
}

/// <summary>
/// Combines several monitor sinks into one sink.
/// </summary>
/// <remarks>
/// Initializes a composite sink.
/// </remarks>
public sealed class CompositeMonitorSink(params IMonitorSink[] sinks) : IMonitorSink
{
    private readonly IMonitorSink[] _sinks = sinks ?? [];

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context)
    {
        var operations = new IMonitorOperation[_sinks.Length];
        for (var index = 0; index < _sinks.Length; index++)
        {
            try { operations[index] = _sinks[index].Begin(context) ?? NullMonitorOperation.Instance; }
            catch { operations[index] = NullMonitorOperation.Instance; }
        }

        return new CompositeMonitorOperation(operations);
    }

    private sealed class CompositeMonitorOperation(IMonitorOperation[] operations) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            foreach (var operation in operations)
            {
                try { operation.Complete(context, returnValue); } catch { }
            }
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            foreach (var operation in operations)
            {
                try { operation.Fail(context, exception); } catch { }
            }
        }

        public void Dispose()
        {
            foreach (var operation in operations)
            {
                try { operation.Dispose(); } catch { }
            }
        }
    }
}
