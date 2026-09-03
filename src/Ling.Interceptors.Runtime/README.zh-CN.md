# Ling.Interceptors.Runtime

[English](README.md) | 简体中文

`Ling.Interceptors` 方法监控的共享运行时契约。它包含公开特性、安全值格式化器、监控生命周期抽象，以及默认的空 sink。

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

在编译调用点的项目中安装 `Ling.Interceptors`。仅声明被监控 API 的程序集可单独引用本运行时包。
