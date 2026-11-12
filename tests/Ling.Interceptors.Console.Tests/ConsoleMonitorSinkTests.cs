using System.Text.Json;
using Xunit;

namespace Ling.Interceptors.Console.Tests;

public sealed class ConsoleMonitorSinkTests
{
    private enum SampleMode
    {
        Fast = 1,
    }

    [Fact]
    public void Writes_structured_json_lines()
    {
        var writer = new StringWriter();
        var sink = new ConsoleMonitorSink(writer);
        var operation = sink.Begin(new MonitorStartContext(
            "Tests",
            "Run",
            new Dictionary<string, MonitorValue>
            {
                ["value"] = new MonitorValue(42),
                ["secret"] = new MonitorValue("ab****yz", isRedacted: true),
                ["mode"] = new MonitorValue(SampleMode.Fast),
            },
            false));
        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(12), true), new MonitorValue("done"));
        operation.Fail(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(15), false), new InvalidOperationException("boom"));

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);

        using var started = JsonDocument.Parse(lines[0]);
        Assert.Equal("started", started.RootElement.GetProperty("kind").GetString());
        Assert.Equal(42, started.RootElement.GetProperty("parameters").GetProperty("value").GetProperty("Value").GetInt32());
        Assert.True(started.RootElement.GetProperty("parameters").GetProperty("secret").GetProperty("IsRedacted").GetBoolean());
        Assert.Equal((int)SampleMode.Fast, started.RootElement.GetProperty("parameters").GetProperty("mode").GetProperty("Value").GetInt32());

        using var completed = JsonDocument.Parse(lines[1]);
        Assert.Equal("completed", completed.RootElement.GetProperty("kind").GetString());
        Assert.Equal("done", completed.RootElement.GetProperty("returnValue").GetString());

        using var failed = JsonDocument.Parse(lines[2]);
        Assert.Equal("failed", failed.RootElement.GetProperty("kind").GetString());
        Assert.Equal(typeof(InvalidOperationException).FullName, failed.RootElement.GetProperty("exception").GetString());
        Assert.Equal("boom", failed.RootElement.GetProperty("message").GetString());
    }
}
