using Ling.Interceptors;
using Ling.Interceptors.CrossAssembly.Target;

MonitorRuntime.Sink = new ConsoleMonitorSink();

var service = new RemoteOrderService();
Console.WriteLine(service.Submit(7, "cross-assembly-secret"));
