namespace Ling.Interceptors;

/// <summary>Marks a static replacement method as an interception handler.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InterceptAttribute : Attribute
{
    public InterceptAttribute(string id, string targetMethod, InterceptionScope scope)
    {
        Id = id;
        TargetMethod = targetMethod;
        Scope = scope;
    }

    public string Id { get; }
    public string TargetMethod { get; }
    public InterceptionScope Scope { get; }
}
