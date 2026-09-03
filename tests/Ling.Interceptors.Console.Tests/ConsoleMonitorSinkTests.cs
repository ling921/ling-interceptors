using Xunit;

namespace Ling.Interceptors.Console.Tests;

public sealed class ConsoleMonitorSinkTests
{
    [Fact]
    public void Writes_structured_json_lines()
    {
        var writer = new StringWriter();
        var sink = new ConsoleMonitorSink(writer);
        var operation = sink.Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue> { ["value"] = new MonitorValue(42) }, false));
        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(12), true), new MonitorValue("done"));

        var output = writer.ToString();
        Assert.Contains("\"started\"", output, StringComparison.Ordinal);
        Assert.Contains("\"completed\"", output, StringComparison.Ordinal);
        Assert.Contains("\"value\"", output, StringComparison.Ordinal);
    }
}
