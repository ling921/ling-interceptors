# Ling.Interceptors.Runtime

English | [简体中文](README.zh-CN.md)

Shared runtime contracts for `Ling.Interceptors` monitoring. It contains the safe value formatter, monitor lifecycle abstractions, and a no-op default sink.

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

Install this package alongside `Ling.Interceptors` in projects that invoke monitored methods. Assemblies that only declare monitored APIs need `Ling.Interceptors.Abstractions` instead.
