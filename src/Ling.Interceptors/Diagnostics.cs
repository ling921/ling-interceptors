using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ling.Interceptors;

internal static class Diagnostics
{
    internal static readonly DiagnosticDescriptor InvalidId = Create("LINGINT001", "Invalid interceptor id", "Interceptor id '{0}' is invalid or duplicated");
    internal static readonly DiagnosticDescriptor InvalidScope = Create("LINGINT002", "Invalid interceptor scope", "GeneratedCode requires Compilation and no unknown scope bits are allowed");
    internal static readonly DiagnosticDescriptor InvalidAttributeArguments = Create("LINGINT003", "Invalid interceptor target", "Use a qualified nameof(T.M) expression for the interceptor target");
    internal static readonly DiagnosticDescriptor InvalidHandler = Create("LINGINT004", "Invalid interceptor handler", "Interceptor handler '{0}' must be an internal or public static method in an accessible type");
    internal static readonly DiagnosticDescriptor TargetResolutionFailed = Create("LINGINT005", "Target resolution failed", "No unique compatible ordinary method '{0}.{1}' was found");
    internal static readonly DiagnosticDescriptor UnknownMarker = Create("LINGINT006", "Unknown interceptor marker", "Interceptor marker '{0}' does not name a rule");
    internal static readonly DiagnosticDescriptor MarkerNotExplicit = Create("LINGINT007", "Marker rule is not explicit", "Interceptor marker '{0}' references a rule without Explicit scope");
    internal static readonly DiagnosticDescriptor MarkerTargetMismatch = Create("LINGINT008", "Marker target mismatch", "Interceptor marker '{0}' does not target invocation '{1}'");
    internal static readonly DiagnosticDescriptor ConflictingCompilationRules = Create("LINGINT009", "Conflicting compilation rules", "Multiple Compilation rules match invocation '{0}'");
    internal static readonly ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(InvalidId, InvalidScope, InvalidAttributeArguments, InvalidHandler, TargetResolutionFailed, UnknownMarker, MarkerNotExplicit, MarkerTargetMismatch, ConflictingCompilationRules);

    private static DiagnosticDescriptor Create(string id, string title, string message) =>
        new(id, title, message, "Ling.Interceptors", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
