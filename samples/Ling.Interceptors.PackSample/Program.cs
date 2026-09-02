using Ling.Interceptors;

var service = new Service();
service.Run("package");
ExternalCaller.Invoke(service);

internal sealed class Service
{
    public void Run(string value) => Console.WriteLine($"original:{value}");
}

internal static class Rules
{
    [Intercept("package-default", nameof(Service.Run), InterceptionScope.Compilation | InterceptionScope.GeneratedCode)]
    internal static void Replace(Service service, string value)
    {
        Console.WriteLine($"intercepted:{value}");
        service.Run(value);
    }
}
