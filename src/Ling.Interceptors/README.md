# Ling.Interceptors

[Project documentation](https://github.com/ling921/ling-interceptors#readme) | [简体中文](https://github.com/ling921/ling-interceptors/blob/master/src/Ling.Interceptors/README.zh-CN.md)

`Ling.Interceptors` provides compile-time C# method-call interception through a Roslyn analyzer and incremental source generator. It requires .NET SDK 9.0.200 or later.

## Installation

```shell
dotnet add package Ling.Interceptors
```

The package automatically configures `Ling.Interceptors.Generated` in `InterceptorsNamespaces`.

## Define a replacement

```csharp
using Ling.Interceptors;

internal static class Replacements
{
    [Intercept("trace", nameof(Service.Send),
        InterceptionScope.Explicit | InterceptionScope.Compilation)]
    internal static void Send(Service service, string value)
    {
        Console.WriteLine($"trace:{value}");
        service.Send(value); // Replacement bodies call the original implementation.
    }
}
```

The target must use qualified `nameof(T.Method)`. The replacement must be an accessible static method with a compatible signature; instance targets add the receiver as the first parameter.

## Select calls

```csharp
service./* intercept:trace */Send("one"); // Explicit rule
service.Send("two");                      // Compilation rule
```

`Explicit` enables an annotated call site. `Compilation` matches ordinary calls in the current project. `GeneratedCode` must be combined with `Compilation`; it covers recognized generated code and enables the package's command-line two-phase compilation when another source generator emits matching calls.

## Diagnostics and limits

The included analyzer reports invalid IDs/scopes, malformed `nameof` expressions, inaccessible or incompatible replacements, ambiguous targets, invalid markers, and conflicting compilation rules. Constructors, properties, operators, delegate invocation, and method-group conversions are unsupported.

## License

[MIT](https://github.com/ling921/ling-interceptors/blob/master/LICENSE)
