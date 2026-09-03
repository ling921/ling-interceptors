# Ling.Interceptors.OpenTelemetry

English | [简体中文](README.zh-CN.md)

OpenTelemetry-compatible trace and metrics integration for `Ling.Interceptors`.

```csharp
MonitorRuntime.Sink = new OpenTelemetryMonitorSink();
```

Configure the host SDK to listen to `Ling.Interceptors`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Ling.Interceptors"))
    .WithMetrics(metrics => metrics.AddMeter("Ling.Interceptors"));
```

The package creates no SDK, provider, or exporter. The application owns telemetry configuration and lifetime.
