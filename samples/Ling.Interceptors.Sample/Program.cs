using Ling.Interceptors;

var target = new Target();
target./* intercept:special */Print(1);
target.Print(2);
Target.Ping("static");

internal sealed class Target
{
    public void Print(int value) => Console.WriteLine($"original:{value}");
    public static void Ping(string value) => Console.WriteLine($"original-static:{value}");
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
