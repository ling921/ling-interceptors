# Ling.Interceptors.Runtime

English | [简体中文](README.zh-CN.md)

Shared runtime contracts for `Ling.Interceptors` monitoring. It contains the public attributes, safe value formatter, monitor lifecycle abstractions, and a no-op default sink.

```csharp
using Ling.Interceptors;

[Monitor(CaptureParameters = true, CaptureReturnValue = true)]
[return: SensitiveData]
public Task<Order> PlaceOrder(
    int customerId,
    [SensitiveData] string product)
{
    // ...
}
```

Install `Ling.Interceptors` in projects that compile call sites. This runtime package is useful on its own for assemblies that only declare monitored APIs.
