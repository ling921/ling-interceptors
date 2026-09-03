namespace Ling.Interceptors;

/// <summary>Defines where an interception or monitoring rule applies.</summary>
[Flags]
public enum InterceptionScope
{
    None = 0,
    Explicit = 1,
    Compilation = 2,
    GeneratedCode = 4,
}
