using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ling.Interceptors;

/// <summary>
/// Emits monitor data through .NET ActivitySource and Meter APIs.
/// </summary>
public sealed class OpenTelemetryMonitorSink : IMonitorSink
{
    /// <summary>
    /// The ActivitySource name applications must register.
    /// </summary>
    public const string ActivitySourceName = "Ling.Interceptors";

    /// <summary>
    /// The Meter name applications must register.
    /// </summary>
    public const string MeterName = "Ling.Interceptors";

    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);
    private static readonly Meter s_meter = new(MeterName);
    private static readonly Histogram<double> s_duration = s_meter.CreateHistogram<double>("ling.interceptors.method.duration", "ms");
    private static readonly Counter<long> s_calls = s_meter.CreateCounter<long>("ling.interceptors.method.calls");
    private static readonly Counter<long> s_failures = s_meter.CreateCounter<long>("ling.interceptors.method.failures");

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context)
    {
        var activity = context.CreateTrace
            ? s_activitySource.StartActivity(context.Category + "." + context.MethodName, ActivityKind.Internal)
            : null;

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("code.namespace", context.Category);
            activity.SetTag("code.function", context.MethodName);
        }

        return new OpenTelemetryMonitorOperation(activity, context.Category, context.MethodName);
    }

    private sealed class OpenTelemetryMonitorOperation(Activity? activity, string category, string methodName) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            Record(context, false);
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);

            Record(context, true);
        }

        public void Dispose()
        {
            activity?.Dispose();
        }

        private void Record(in MonitorCompletionContext context, bool failed)
        {
            var tags = new TagList
            {
                { "code.namespace", category },
                { "code.function", methodName },
            };
            s_calls.Add(1, tags);
            if (failed)
                s_failures.Add(1, tags);
            if (context.RecordTiming)
                s_duration.Record(context.Duration.TotalMilliseconds, tags);
        }
    }
}
