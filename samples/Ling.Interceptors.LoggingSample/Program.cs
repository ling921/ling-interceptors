using Ling.Interceptors;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddSimpleConsole(options => options.SingleLine = true)
    .SetMinimumLevel(LogLevel.Debug));

MonitorRuntime.Sink = new LoggerMonitorSink(loggerFactory);

var service = new OrderService();
Console.WriteLine(await service.PlaceOrder(42, "very-secret-product"));

internal sealed class OrderService
{
    [Monitor(CaptureParameters = true, CaptureReturnValue = true, CreateTrace = true)]
    public async Task<string> PlaceOrder(int customerId, [SensitiveData] string product)
    {
        await Task.Yield();
        return $"order for {customerId}: {product}";
    }
}
