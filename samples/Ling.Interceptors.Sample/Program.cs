using Ling.Interceptors;

MonitorRuntime.Sink = new ConsoleMonitorSink();

var target = new Target();
target./* intercept:special */Print(1);
target.Print(2);
Target.Ping("static");
Console.WriteLine(target.Measure("sample"));
Console.WriteLine(await target.MeasureAsync("sample"));
Console.WriteLine(await target.MeasureValueAsync("sample"));

internal sealed class Target
{
    public void Print(int value) => Console.WriteLine($"original:{value}");
    public static void Ping(string value) => Console.WriteLine($"original-static:{value}");

    [Monitor(CaptureParameters = true, CaptureReturnValue = true)]
    public string Measure([SensitiveData] string value) => $"measured:{value}";

    [Monitor(CaptureParameters = true, CaptureReturnValue = true, CreateTrace = true)]
    public async Task<string> MeasureAsync([SensitiveData] string value)
    {
        await Task.Yield();
        return $"measured-async:{value}";
    }

    [Monitor(CaptureReturnValue = true)]
    public async ValueTask<string> MeasureValueAsync(string value)
    {
        await Task.Yield();
        return $"measured-value-task:{value}";
    }
}

internal static class Replacements
{
    [Intercept("default", nameof(Target.Print), InterceptionScope.Compilation)]
    internal static void Default(Target target, int value)
    {
        Console.WriteLine($"default:{value}");
        target.Print(value);
    }

    [Intercept("special", nameof(Target.Print), InterceptionScope.Explicit)]
    internal static void Special(Target target, int value)
    {
        Console.WriteLine($"special:{value}");
    }

    [Intercept("static", nameof(Target.Ping), InterceptionScope.Compilation)]
    internal static void StaticReplacement(string value)
    {
        Console.WriteLine($"static:{value}");
        Target.Ping(value);
    }
}
