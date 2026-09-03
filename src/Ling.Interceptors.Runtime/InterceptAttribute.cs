namespace Ling.Interceptors;

/// <summary>
/// Marks a static replacement method as an interception handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InterceptAttribute : Attribute
{
    /// <summary>
    /// Initializes an interception rule.
    /// </summary>
    public InterceptAttribute(string id, string targetMethod, InterceptionScope scope)
    {
        Id = id;
        TargetMethod = targetMethod;
        Scope = scope;
    }

    /// <summary>
    /// Gets the stable rule identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the qualified target method name.
    /// </summary>
    public string TargetMethod { get; }

    /// <summary>
    /// Gets the rule scope.
    /// </summary>
    public InterceptionScope Scope { get; }
}
