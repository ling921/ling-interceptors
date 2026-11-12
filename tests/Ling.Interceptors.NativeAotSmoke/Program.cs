using System.Text.Json;
using Ling.Interceptors;
using Microsoft.Extensions.Logging.Abstractions;

var output = new StringWriter();
var consoleSink = new ConsoleMonitorSink(output);
var loggerSink = new LoggerMonitorSink(NullLoggerFactory.Instance);
var otelSink = new OpenTelemetryMonitorSink();

MonitorRuntime.Sink = consoleSink;
var target = new Target();
var special = target./* intercept:special */Compute(5);
var standard = target.Compute(3);
var monitored = target.Measure("secret", SampleKind.Beta);
var asyncResult = await target.MeasureAsync("token");

var otelOp = otelSink.Begin(new MonitorStartContext("Smoke", "Ping", new Dictionary<string, MonitorValue>(), false));
otelOp.Complete(new MonitorCompletionContext("Smoke", "Ping", TimeSpan.FromMilliseconds(1), true), new MonitorValue(true));
var logOp = loggerSink.Begin(new MonitorStartContext("Smoke", "Ping", new Dictionary<string, MonitorValue> { ["n"] = new MonitorValue(1) }, false));
logOp.Complete(new MonitorCompletionContext("Smoke", "Ping", TimeSpan.FromMilliseconds(1), false), null);

var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
if (special != 500
    || standard != 13
    || monitored != "measured:secret:Beta"
    || asyncResult != "measured-async:token"
    || lines.Length < 4)
{
    return 1;
}

using (var started = JsonDocument.Parse(lines[0]))
{
    if (started.RootElement.GetProperty("kind").GetString() != "started"
        || started.RootElement.GetProperty("parameters").GetProperty("value").GetProperty("IsRedacted").GetBoolean() != true)
    {
        return 2;
    }
}

Console.WriteLine("Ling.Interceptors Native AOT smoke passed.");
return 0;

internal enum SampleKind
{
    Alpha = 0,
    Beta = 1,
}

internal sealed class Target
{
    public int Compute(int value) => value + 10;

    [Monitor(CaptureParameters = true, CaptureReturnValue = true)]
    public string Measure([SensitiveData] string value, SampleKind kind) => $"measured:{value}:{kind}";

    [Monitor(CaptureParameters = true, CaptureReturnValue = true, CreateTrace = true)]
    public async Task<string> MeasureAsync([SensitiveData] string value)
    {
        await Task.Yield();
        return $"measured-async:{value}";
    }
}

internal static class Replacements
{
    [Intercept("special", nameof(Target.Compute), InterceptionScope.Explicit)]
    internal static int Special(Target target, int value) => value * 100;
}
