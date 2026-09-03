# Ling.Interceptors.Abstractions

[English](README.md) | 简体中文

`Ling.Interceptors` 各包共享的零依赖公开特性：`InterceptAttribute`、`MonitorAttribute`、`SensitiveDataAttribute` 与 `InterceptionScope`。

使用方法替换时安装 `Ling.Interceptors` 即可。调用 `[Monitor]` 方法的项目还需要安装 `Ling.Interceptors.Runtime`，因为生成的监控包装代码依赖运行时契约。
