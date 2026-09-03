using Microsoft.Extensions.Logging;

namespace Ling.Interceptors;

/// <summary>
/// Writes monitor lifecycle events through <see cref="ILogger"/>.
/// </summary>
public sealed class LoggerMonitorSink : IMonitorSink
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly LogLevel _logLevel;

    /// <summary>
    /// Initializes a logger-backed monitor sink.
    /// </summary>
    public LoggerMonitorSink(ILoggerFactory loggerFactory, LogLevel logLevel = LogLevel.Debug)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logLevel = logLevel;
    }

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context)
    {
        var logger = _loggerFactory.CreateLogger(context.Category);
        if (logger.IsEnabled(_logLevel))
        {
            logger.Log(_logLevel, "Monitor started {Method} with parameters {Parameters}", context.MethodName, ToSafeValues(context.Parameters));
        }

        return new LoggerMonitorOperation(logger, _logLevel, context.MethodName);
    }

    private static IReadOnlyDictionary<string, object?> ToSafeValues(IReadOnlyDictionary<string, MonitorValue> values)
    {
        var result = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
            result[pair.Key] = pair.Value.Value;
        return result;
    }

    private sealed class LoggerMonitorOperation(ILogger logger, LogLevel logLevel, string methodName) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            if (!logger.IsEnabled(logLevel))
                return;

            logger.Log(
                logLevel,
                "Monitor completed {Method} in {DurationMs} ms with return value {ReturnValue}",
                methodName,
                context.Duration.TotalMilliseconds,
                returnValue?.Value);
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            logger.LogError(
                exception,
                "Monitor failed {Method} in {DurationMs} ms",
                methodName,
                context.Duration.TotalMilliseconds);
        }

        public void Dispose()
        {
        }
    }
}
