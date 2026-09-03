namespace Ling.Interceptors;

/// <summary>Marks a parameter or return value as sensitive.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = false)]
public sealed class SensitiveDataAttribute : Attribute
{
}
