# Ling.Interceptors

[项目文档](https://github.com/ling921/ling-interceptors#readme) | [English](https://github.com/ling921/ling-interceptors/blob/master/src/Ling.Interceptors/README.md)

`Ling.Interceptors` 通过 Roslyn 分析器和增量源生成器实现 C# 方法调用的编译期拦截与监控，要求 .NET SDK 9.0.200 或更高版本。

## 安装

```shell
dotnet add package Ling.Interceptors
```

包会自动在 `InterceptorsNamespaces` 中配置 `Ling.Interceptors.Generated`。

## 监控调用

```csharp
[Monitor(CaptureParameters = true, CaptureReturnValue = true)]
[return: SensitiveData]
public async Task<Order> PlaceOrder(
    int customerId,
    [SensitiveData] string product)
{
    // implementation
}
```

generator 会查找当前 compilation 内的调用并自动生成 wrapper，记录选择的值、异常和耗时。应用需要显式配置 runtime sink：

```csharp
MonitorRuntime.Sink = new LoggerMonitorSink(loggerFactory);
```

如果程序集只声明受监控 API，可只引用 `Ling.Interceptors.Runtime`。包含调用点的项目必须引用本包，以运行 generator。

## 声明替换方法

```csharp
using Ling.Interceptors;

internal static class Replacements
{
    [Intercept("trace", nameof(Service.Send),
        InterceptionScope.Explicit | InterceptionScope.Compilation)]
    internal static void Send(Service service, string value)
    {
        Console.WriteLine($"trace:{value}");
        service.Send(value); // 替换方法体直接调用原始实现
    }
}
```

目标必须使用限定形式 `nameof(T.Method)`。替换方法必须可访问、为静态方法且签名兼容；实例目标需将接收者作为第一个参数。

## 选择调用点

```csharp
service./* intercept:trace */Send("one"); // Explicit 规则
service.Send("two");                      // Compilation 规则
```

`Explicit` 允许调用点用注释选择规则。`Compilation` 匹配当前项目中的普通调用。`GeneratedCode` 必须与 `Compilation` 组合；它覆盖可识别的生成代码，并在其他源生成器产生匹配调用时启用命令行两阶段编译。

## 诊断与限制

随包提供的分析器会报告无效 ID/Scope、错误的 `nameof`、不可访问或不兼容的替换方法、歧义目标、无效标记、冲突的 compilation 规则及 Monitor/拦截冲突。不支持构造函数、属性、运算符、委托调用和方法组转换。方法监控支持普通返回值、`Task`、`Task<T>`、`ValueTask` 和 `ValueTask<T>`。

## 协议

[MIT](https://github.com/ling921/ling-interceptors/blob/master/LICENSE)
