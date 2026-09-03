using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ling.Interceptors;

/// <summary>
/// Formats a captured value before it is passed to a monitor sink.
/// </summary>
public interface IMonitorValueFormatter
{
    /// <summary>
    /// Formats a value into a safe, structured representation.
    /// </summary>
    MonitorValue Format(object? value, in MonitorValueContext context);
}

/// <summary>
/// Default safe formatter for generated monitor wrappers.
/// </summary>
public sealed class DefaultMonitorValueFormatter : IMonitorValueFormatter
{
    private const int MaskVisibleCharacters = 2;
    private readonly JsonSerializerOptions _options = new()
    {
        MaxDepth = 8,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    /// <summary>Gets the shared formatter instance.</summary>
    public static DefaultMonitorValueFormatter Instance { get; } = new DefaultMonitorValueFormatter();

    /// <inheritdoc />
    public MonitorValue Format(object? value, in MonitorValueContext context)
    {
        if (context.IsSensitive)
        {
            return new MonitorValue(value is string text ? Mask(text) : "[REDACTED]", true);
        }

        if (value is null || value is string || value.GetType().IsPrimitive || value is decimal || value is DateTime || value is DateTimeOffset || value is TimeSpan || value is Guid || value.GetType().IsEnum)
        {
            return new MonitorValue(value);
        }

        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), _options));
            return new MonitorValue(document.RootElement.Clone());
        }
        catch
        {
            return new MonitorValue("<" + context.DeclaredType.FullName + ":unserializable>");
        }
    }

    private static string Mask(string value)
    {
        return value.Length <= MaskVisibleCharacters * 2
            ? "****"
            : value.Substring(0, MaskVisibleCharacters) + "****" + value.Substring(value.Length - MaskVisibleCharacters, MaskVisibleCharacters);
    }
}
