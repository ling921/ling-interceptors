# Ling.Interceptors.Logging

English | [简体中文](README.zh-CN.md)

`ILogger` integration for `Ling.Interceptors` monitoring.

```csharp
MonitorRuntime.Sink = new LoggerMonitorSink(loggerFactory);
```

The package depends only on `Microsoft.Extensions.Logging.Abstractions`; applications select their own logging provider and configuration.
