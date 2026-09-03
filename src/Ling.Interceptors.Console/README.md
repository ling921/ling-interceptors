# Ling.Interceptors.Console

English | [简体中文](README.zh-CN.md)

Console JSON Lines integration for `Ling.Interceptors` monitoring.

```csharp
MonitorRuntime.Sink = new ConsoleMonitorSink();
```

The sink writes to standard error and is never configured automatically by the core package.
