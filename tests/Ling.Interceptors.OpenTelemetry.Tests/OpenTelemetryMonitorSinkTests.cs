using System.Diagnostics;
using System.Diagnostics.Metrics;
using Xunit;

namespace Ling.Interceptors.OpenTelemetry.Tests;

public sealed class OpenTelemetryMonitorSinkTests
{
    [Fact]
    public void Creates_activity_when_requested()
    {
        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryMonitorSink.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = _ => started++,
        };
        ActivitySource.AddActivityListener(listener);

        var operation = new OpenTelemetryMonitorSink().Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue>(), true));
        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(1), true), null);
        operation.Dispose();

        Assert.Equal(1, started);
    }

    [Fact]
    public void Records_duration_metric()
    {
        var durations = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OpenTelemetryMonitorSink.MeterName && instrument.Name == "ling.interceptors.method.duration")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => durations.Add(measurement));
        listener.Start();

        var operation = new OpenTelemetryMonitorSink().Begin(new MonitorStartContext("Tests", "Run", new Dictionary<string, MonitorValue>(), false));
        operation.Complete(new MonitorCompletionContext("Tests", "Run", TimeSpan.FromMilliseconds(12), true), null);

        Assert.Contains(12d, durations);
    }
}
