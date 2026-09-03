namespace Ling.Interceptors;

/// <summary>
/// Formats a captured value before it is passed to a monitor sink.
/// </summary>
public interface IMonitorValueFormatter
{
    /// <summary>
/// Formats a value into a safe representation. The context identifies sensitive values.
    /// </summary>
    MonitorValue Format(object? value, in MonitorValueContext context);
}

/// <summary>
/// Default safe formatter for generated monitor wrappers.
/// </summary>
public sealed class DefaultMonitorValueFormatter : IMonitorValueFormatter
{
    private const int MaskVisibleCharacters = 2;
    /// <summary>Gets the shared formatter instance.</summary>
    public static DefaultMonitorValueFormatter Instance { get; } = new DefaultMonitorValueFormatter();

    /// <inheritdoc />
    public MonitorValue Format(object? value, in MonitorValueContext context)
    {
        if (context.IsSensitive)
        {
            return new MonitorValue(value is string text ? Mask(text) : "[REDACTED]", true);
        }

        if (value is null || value is string || IsScalar(value))
        {
            return new MonitorValue(value);
        }

        return new MonitorValue("<" + (context.DeclaredType.FullName ?? context.DeclaredType.Name) + ">");
    }

    private static bool IsScalar(object value)
        => value.GetType().IsPrimitive || value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid || value.GetType().IsEnum;

    private static string Mask(string value)
    {
        return value.Length <= MaskVisibleCharacters * 2
            ? "****"
            : value.Substring(0, MaskVisibleCharacters) + "****" + value.Substring(value.Length - MaskVisibleCharacters, MaskVisibleCharacters);
    }
}
