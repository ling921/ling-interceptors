# Ling.Interceptors.Runtime

[English](README.md) | 简体中文

`Ling.Interceptors` 方法监控的共享运行时契约。它包含安全值格式化器、监控生命周期抽象，以及默认的空 sink。

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

调用受监控方法的项目应将本包与 `Ling.Interceptors` 一起安装。仅声明受监控 API 的程序集应改为引用 `Ling.Interceptors.Abstractions`。
