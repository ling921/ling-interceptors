# Ling.Interceptors.Logging

[English](README.md) | 简体中文

为 `Ling.Interceptors` 方法监控提供 `ILogger` 集成。

```csharp
MonitorRuntime.Sink = new LoggerMonitorSink(loggerFactory);
```

该包仅依赖 `Microsoft.Extensions.Logging.Abstractions`；日志提供程序和配置完全由应用程序选择并管理。
