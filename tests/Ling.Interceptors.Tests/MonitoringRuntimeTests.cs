using Xunit;

namespace Ling.Interceptors.Tests;

public sealed class MonitoringRuntimeTests
{
    private static readonly object s_runtimeGate = new();

    [Fact]
    public void Default_formatter_masks_sensitive_values_without_calling_to_string()
    {
        var formatter = DefaultMonitorValueFormatter.Instance;
        var stringValue = formatter.Format("secret", new MonitorValueContext("password", typeof(string), true));
        var objectValue = formatter.Format(new ThrowingToString(), new MonitorValueContext("request", typeof(ThrowingToString), true));

        Assert.Equal("se****et", stringValue.Value);
        Assert.Equal("[REDACTED]", objectValue.Value);
        Assert.True(objectValue.IsRedacted);
    }

    [Fact]
    public void Default_formatter_uses_a_type_summary_for_complex_values_without_calling_to_string()
    {
        var value = DefaultMonitorValueFormatter.Instance.Format(new ThrowingToString(), new MonitorValueContext("request", typeof(ThrowingToString), false));

        Assert.Equal("<Ling.Interceptors.Tests.MonitoringRuntimeTests+ThrowingToString>", value.Value);
        Assert.False(value.IsRedacted);
    }

    [Fact]
    public void Monitor_runtime_passes_the_sensitive_context_to_custom_formatters()
    {
        lock (s_runtimeGate)
        {
            var previous = MonitorRuntime.Formatter;
            var formatter = new RecordingFormatter();
            try
            {
                MonitorRuntime.Formatter = formatter;
                MonitorRuntime.Format("secret", new MonitorValueContext("password", typeof(string), true));

                Assert.True(formatter.Context.IsSensitive);
                Assert.Equal("password", formatter.Context.Name);
                Assert.Equal(typeof(string), formatter.Context.DeclaredType);
            }
            finally { MonitorRuntime.Formatter = previous; }
        }
    }

    [Fact]
    public void Monitor_runtime_swallows_sink_failures()
    {
        lock (s_runtimeGate)
        {
            var previous = MonitorRuntime.Sink;
            try
            {
                MonitorRuntime.Sink = new ThrowingSink();
                var operation = MonitorRuntime.Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue>(), false));
                operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.Zero, true), null);
                operation.Fail(new MonitorCompletionContext("Tests", "Run", TimeSpan.Zero, true), new InvalidOperationException());
                operation.Dispose();
            }
            finally { MonitorRuntime.Sink = previous; }
        }
    }

    [Fact]
    public void Composite_sink_forwards_the_full_lifecycle_to_every_sink()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var operation = new CompositeMonitorSink(first, second).Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue>(), false));

        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.Zero, true), null);
        operation.Fail(new MonitorCompletionContext("Tests", "Run", TimeSpan.Zero, true), new InvalidOperationException());
        operation.Dispose();

        Assert.Equal(["begin", "complete", "fail", "dispose"], first.Events);
        Assert.Equal(first.Events, second.Events);
    }

    private sealed class ThrowingToString { public override string ToString() => throw new InvalidOperationException(); }
    private sealed class RecordingFormatter : IMonitorValueFormatter
    {
        public MonitorValueContext Context { get; private set; }
        public MonitorValue Format(object? value, in MonitorValueContext context)
        {
            Context = context;
            return new MonitorValue("formatted", context.IsSensitive);
        }
    }
    private sealed class ThrowingSink : IMonitorSink { public IMonitorOperation Begin(in MonitorStartContext context) => throw new InvalidOperationException(); }
    private sealed class RecordingSink : IMonitorSink
    {
        public List<string> Events { get; } = [];
        public IMonitorOperation Begin(in MonitorStartContext context) { Events.Add("begin"); return new RecordingOperation(Events); }
    }
    private sealed class RecordingOperation(List<string> events) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue) => events.Add("complete");
        public void Fail(in MonitorCompletionContext context, Exception exception) => events.Add("fail");
        public void Dispose() => events.Add("dispose");
    }
}
