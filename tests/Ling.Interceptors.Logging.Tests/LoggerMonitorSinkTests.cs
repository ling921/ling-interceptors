using Microsoft.Extensions.Logging;
using Xunit;

namespace Ling.Interceptors.Logging.Tests;

public sealed class LoggerMonitorSinkTests
{
    [Fact]
    public void Emits_start_and_completion_messages()
    {
        var factory = new RecordingLoggerFactory();
        var sink = new LoggerMonitorSink(factory);
        var operation = sink.Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue>(), false));
        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(1), true), null);

        Assert.Contains(factory.Messages, message => message.Contains("started", StringComparison.Ordinal));
        Assert.Contains(factory.Messages, message => message.Contains("completed", StringComparison.Ordinal));
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = [];
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }
    }

    private sealed class RecordingLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
