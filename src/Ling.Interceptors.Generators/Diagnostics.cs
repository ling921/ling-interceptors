using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ling.Interceptors;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor InvalidId = Create("LINGINT001", "Invalid interceptor id", "Interceptor id '{0}' is invalid or duplicated");
    public static readonly DiagnosticDescriptor InvalidScope = Create("LINGINT002", "Invalid interceptor scope", "GeneratedCode requires Compilation and no unknown scope bits are allowed");
    public static readonly DiagnosticDescriptor InvalidAttributeArguments = Create("LINGINT003", "Invalid interceptor target", "Use a qualified nameof(T.M) expression for the interceptor target");
    public static readonly DiagnosticDescriptor InvalidHandler = Create("LINGINT004", "Invalid interceptor handler", "Interceptor handler '{0}' must be an internal or public static method in an accessible type");
    public static readonly DiagnosticDescriptor TargetResolutionFailed = Create("LINGINT005", "Target resolution failed", "No unique compatible ordinary method '{0}.{1}' was found");
    public static readonly DiagnosticDescriptor UnknownMarker = Create("LINGINT006", "Unknown interceptor marker", "Interceptor marker '{0}' does not name a rule");
    public static readonly DiagnosticDescriptor MarkerNotExplicit = Create("LINGINT007", "Marker rule is not explicit", "Interceptor marker '{0}' references a rule without Explicit scope");
    public static readonly DiagnosticDescriptor MarkerTargetMismatch = Create("LINGINT008", "Marker target mismatch", "Interceptor marker '{0}' does not target invocation '{1}'");
    public static readonly DiagnosticDescriptor ConflictingCompilationRules = Create("LINGINT009", "Conflicting compilation rules", "Multiple Compilation rules match invocation '{0}'");
    public static readonly DiagnosticDescriptor InvalidMonitorScope = Create("LINGINT010", "Invalid monitor scope", "Monitor '{0}' must use Compilation and may optionally include GeneratedCode");
    public static readonly DiagnosticDescriptor ConflictingMonitorRule = Create("LINGINT011", "Conflicting monitor rule", "Monitor and interception rules both match invocation '{0}'");
    public static readonly DiagnosticDescriptor UnsupportedMonitorTarget = Create("LINGINT012", "Unsupported monitor target", "Monitor '{0}' cannot target a ref-return method");
    public static readonly DiagnosticDescriptor MissingMonitoringRuntime = Create("LINGINT013", "Monitoring runtime is required", "Invocation '{0}' targets a monitored method; add a reference to Ling.Interceptors.Runtime");
    public static readonly ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(InvalidId, InvalidScope, InvalidAttributeArguments, InvalidHandler, TargetResolutionFailed, UnknownMarker, MarkerNotExplicit, MarkerTargetMismatch, ConflictingCompilationRules, InvalidMonitorScope, ConflictingMonitorRule, UnsupportedMonitorTarget, MissingMonitoringRuntime);

    private static DiagnosticDescriptor Create(string id, string title, string message)
        => new(id, title, message, "Ling.Interceptors", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
