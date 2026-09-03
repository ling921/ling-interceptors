using System.Text.Json;

namespace Ling.Interceptors;

/// <summary>
/// Writes monitor lifecycle events as JSON Lines to standard error.
/// </summary>
public sealed class ConsoleMonitorSink : IMonitorSink
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a sink that writes to <see cref="Console.Error"/>.
    /// </summary>
    public ConsoleMonitorSink() : this(Console.Error)
    {
    }

    /// <summary>
    /// Initializes a sink that writes to a supplied writer.
    /// </summary>
    public ConsoleMonitorSink(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context)
    {
        Write(new
        {
            kind = "started",
            category = context.Category,
            method = context.MethodName,
            parameters = context.Parameters,
        });

        return new ConsoleMonitorOperation(this, context.Category, context.MethodName);
    }

    private void Write(object value)
    {
        lock (_gate)
        {
            _writer.WriteLine(JsonSerializer.Serialize(value));
        }
    }

    private sealed class ConsoleMonitorOperation(ConsoleMonitorSink sink, string category, string methodName) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            sink.Write(new
            {
                kind = "completed",
                category = category,
                method = methodName,
                durationMs = context.Duration.TotalMilliseconds,
                returnValue = returnValue?.Value,
            });
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            sink.Write(new
            {
                kind = "failed",
                category = category,
                method = methodName,
                durationMs = context.Duration.TotalMilliseconds,
                exception = exception.GetType().FullName,
                message = exception.Message,
            });
        }

        public void Dispose()
        {
        }
    }
}
