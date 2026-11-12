using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ling.Interceptors;

/// <summary>
/// Writes monitor lifecycle events as JSON Lines to standard error.
/// </summary>
public sealed class ConsoleMonitorSink : IMonitorSink
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a sink that writes to <see cref="Console.Error"/>.
    /// </summary>
    public ConsoleMonitorSink() : this(Console.Error)
    {
    }

    /// <summary>
    /// Initializes a sink that writes to a supplied writer.
    /// </summary>
    public ConsoleMonitorSink(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <inheritdoc />
    public IMonitorOperation Begin(in MonitorStartContext context)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("kind", "started");
            json.WriteString("category", context.Category);
            json.WriteString("method", context.MethodName);
            json.WritePropertyName("parameters");
            json.WriteStartObject();
            foreach (var pair in context.Parameters)
            {
                json.WritePropertyName(pair.Key);
                json.WriteStartObject();
                json.WritePropertyName("Value");
                WriteDynamicValue(json, pair.Value.Value);
                json.WriteBoolean("IsRedacted", pair.Value.IsRedacted);
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        WriteLine(buffer);
        return new ConsoleMonitorOperation(this, context.Category, context.MethodName);
    }

    private void WriteLine(MemoryStream buffer)
    {
        var line = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    private static void WriteDynamicValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                break;
            case string text:
                json.WriteStringValue(text);
                break;
            case bool boolean:
                json.WriteBooleanValue(boolean);
                break;
            case byte number:
                json.WriteNumberValue(number);
                break;
            case sbyte number:
                json.WriteNumberValue(number);
                break;
            case short number:
                json.WriteNumberValue(number);
                break;
            case ushort number:
                json.WriteNumberValue(number);
                break;
            case int number:
                json.WriteNumberValue(number);
                break;
            case uint number:
                json.WriteNumberValue(number);
                break;
            case long number:
                json.WriteNumberValue(number);
                break;
            case ulong number:
                json.WriteNumberValue(number);
                break;
            case float number:
                json.WriteNumberValue(number);
                break;
            case double number:
                json.WriteNumberValue(number);
                break;
            case decimal number:
                json.WriteNumberValue(number);
                break;
            case DateTime dateTime:
                json.WriteStringValue(dateTime);
                break;
            case DateTimeOffset dateTimeOffset:
                json.WriteStringValue(dateTimeOffset);
                break;
            case Guid guid:
                json.WriteStringValue(guid);
                break;
            case Enum enumValue when Type.GetTypeCode(enumValue.GetType()) == TypeCode.UInt64:
                json.WriteNumberValue(Convert.ToUInt64(enumValue, CultureInfo.InvariantCulture));
                break;
            case Enum enumValue:
                json.WriteNumberValue(Convert.ToInt64(enumValue, CultureInfo.InvariantCulture));
                break;
            default:
                json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString());
                break;
        }
    }

    private sealed class ConsoleMonitorOperation(ConsoleMonitorSink sink, string category, string methodName) : IMonitorOperation
    {
        public void Complete(in MonitorCompletionContext context, MonitorValue? returnValue)
        {
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("kind", "completed");
                json.WriteString("category", category);
                json.WriteString("method", methodName);
                json.WriteNumber("durationMs", context.Duration.TotalMilliseconds);
                json.WritePropertyName("returnValue");
                WriteDynamicValue(json, returnValue?.Value);
                json.WriteEndObject();
            }

            sink.WriteLine(buffer);
        }

        public void Fail(in MonitorCompletionContext context, Exception exception)
        {
            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("kind", "failed");
                json.WriteString("category", category);
                json.WriteString("method", methodName);
                json.WriteNumber("durationMs", context.Duration.TotalMilliseconds);
                json.WriteString("exception", exception.GetType().FullName);
                json.WriteString("message", exception.Message);
                json.WriteEndObject();
            }

            sink.WriteLine(buffer);
        }

        public void Dispose()
        {
        }
    }
}
