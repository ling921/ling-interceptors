using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ling.Interceptors;

internal sealed class Rule
{
    public Rule(string id, ITypeSymbol targetType, string targetName, int scope, IMethodSymbol handler, IMethodSymbol? target, Location attributeLocation, bool usesTypeofAndNameof)
    {
        Id = id;
        TargetType = targetType;
        TargetName = targetName;
        Scope = scope;
        Handler = handler;
        Target = target;
        AttributeLocation = attributeLocation;
        UsesTypeofAndNameof = usesTypeofAndNameof;
    }

    public string Id { get; }
    public ITypeSymbol TargetType { get; }
    public string TargetName { get; }
    public int Scope { get; }
    public IMethodSymbol Handler { get; }
    public IMethodSymbol? Target { get; }
    public Location AttributeLocation { get; }
    public bool UsesTypeofAndNameof { get; }

    public Rule WithTarget(IMethodSymbol target) => new(Id, TargetType, TargetName, Scope, Handler, target, AttributeLocation, UsesTypeofAndNameof);
}

internal sealed class Invocation
{
    public Invocation(IMethodSymbol target, InterceptableLocation location, Location diagnosticLocation, string? markerId, bool isGenerated, IMethodSymbol? containingMethod)
    {
        Target = target;
        Location = location;
        DiagnosticLocation = diagnosticLocation;
        MarkerId = markerId;
        IsGenerated = isGenerated;
        ContainingMethod = containingMethod;
    }

    public IMethodSymbol Target { get; }
    public InterceptableLocation Location { get; }
    public Location DiagnosticLocation { get; }
    public string? MarkerId { get; }
    public bool IsGenerated { get; }
    public IMethodSymbol? ContainingMethod { get; }
}

internal sealed class MonitorRule
{
    public MonitorRule(
        IMethodSymbol target,
        int scope,
        bool captureParameters,
        bool captureReturnValue,
        bool captureExceptions,
        bool recordTiming,
        bool createTrace,
        Location diagnosticLocation)
    {
        Target = target;
        Scope = scope;
        CaptureParameters = captureParameters;
        CaptureReturnValue = captureReturnValue;
        CaptureExceptions = captureExceptions;
        RecordTiming = recordTiming;
        CreateTrace = createTrace;
        DiagnosticLocation = diagnosticLocation;
    }

    public IMethodSymbol Target { get; }
    public int Scope { get; }
    public bool CaptureParameters { get; }
    public bool CaptureReturnValue { get; }
    public bool CaptureExceptions { get; }
    public bool RecordTiming { get; }
    public bool CreateTrace { get; }
    public Location DiagnosticLocation { get; }
}
