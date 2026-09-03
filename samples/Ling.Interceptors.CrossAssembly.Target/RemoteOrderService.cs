using Ling.Interceptors;

namespace Ling.Interceptors.CrossAssembly.Target;

public sealed class RemoteOrderService
{
    [Monitor(CaptureParameters = true, CaptureReturnValue = true)]
    public string Submit(int customerId, [SensitiveData] string product) => $"submitted {customerId}: {product}";
}
