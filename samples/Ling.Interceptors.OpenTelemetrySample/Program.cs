using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ling.Interceptors;

using var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == OpenTelemetryMonitorSink.ActivitySourceName,
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => Console.WriteLine($"trace: {activity.DisplayName} ({activity.Duration.TotalMilliseconds:F1} ms)"),
};
ActivitySource.AddActivityListener(activityListener);

using var meterListener = new MeterListener();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == OpenTelemetryMonitorSink.MeterName)
        listener.EnableMeasurementEvents(instrument);
};
meterListener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
    Console.WriteLine($"metric: {instrument.Name} = {measurement:F1} {instrument.Unit}"));
meterListener.Start();

MonitorRuntime.Sink = new OpenTelemetryMonitorSink();
Console.WriteLine(new WeatherService().GetForecast("Hong Kong"));

internal sealed class WeatherService
{
    [Monitor(CaptureParameters = true, CaptureReturnValue = true, CreateTrace = true)]
    public string GetForecast(string city) => $"Sunny in {city}";
}
