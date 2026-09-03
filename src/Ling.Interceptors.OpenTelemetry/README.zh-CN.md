# Ling.Interceptors.OpenTelemetry

[English](README.md) | 简体中文

为 `Ling.Interceptors` 提供兼容 OpenTelemetry 的追踪与指标集成。

```csharp
MonitorRuntime.Sink = new OpenTelemetryMonitorSink();
```

在宿主应用中注册 `Ling.Interceptors` 的 source 和 meter：

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Ling.Interceptors"))
    .WithMetrics(metrics => metrics.AddMeter("Ling.Interceptors"));
```

该包不会创建 SDK、provider 或 exporter；遥测配置与生命周期由应用程序负责。
