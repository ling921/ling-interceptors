# Ling.Interceptors.Abstractions

English | [简体中文](README.zh-CN.md)

Zero-dependency public attributes shared by `Ling.Interceptors` packages: `InterceptAttribute`, `MonitorAttribute`, `SensitiveDataAttribute`, and `InterceptionScope`.

Use `Ling.Interceptors` for method replacement. Add `Ling.Interceptors.Runtime` to a caller project before it invokes a `[Monitor]` method, because generated monitor wrappers require the runtime contracts.
