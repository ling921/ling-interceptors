# Ling.Interceptors.Console

[English](README.md) | 简体中文

为 `Ling.Interceptors` 方法监控提供 Console JSON Lines 集成。

```csharp
MonitorRuntime.Sink = new ConsoleMonitorSink();
```

此 sink 将 JSON Lines 写入标准错误输出，核心包不会自动配置它。
