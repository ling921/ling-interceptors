using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ling.Interceptors;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterceptorsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterCompilationStartAction(start =>
        {
            var rules = new ConcurrentBag<Rule>();
            var monitors = new ConcurrentBag<MonitorRule>();
            var calls = new ConcurrentBag<Invocation>();
            start.RegisterSyntaxNodeAction(analysis => AddRule(analysis, rules), SyntaxKind.MethodDeclaration);
            start.RegisterSyntaxNodeAction(analysis => AddMonitor(analysis, monitors), SyntaxKind.MethodDeclaration);
            start.RegisterSyntaxNodeAction(analysis => AddCall(analysis, calls), SyntaxKind.InvocationExpression);
            var hasMonitoringRuntime = start.Compilation.GetTypeByMetadataName("Ling.Interceptors.MonitorRuntime") is not null;
            start.RegisterCompilationEndAction(analysis => Analyze(rules.ToImmutableArray(), monitors.ToImmutableArray(), calls.ToImmutableArray(), hasMonitoringRuntime, analysis.ReportDiagnostic));
        });
    }

    private static void AddRule(SyntaxNodeAnalysisContext analysis, ConcurrentBag<Rule> rules)
    {
        var rule = InterceptorsGenerator.TryCreateRule(analysis.SemanticModel, (MethodDeclarationSyntax)analysis.Node, analysis.CancellationToken);
        if (rule is not null)
            rules.Add(rule);
    }

    private static void AddCall(SyntaxNodeAnalysisContext analysis, ConcurrentBag<Invocation> calls)
    {
        var call = InterceptorsGenerator.TryCreateInvocation(analysis.SemanticModel, (InvocationExpressionSyntax)analysis.Node, analysis.CancellationToken);
        if (call is not null)
            calls.Add(call);
    }

    private static void AddMonitor(SyntaxNodeAnalysisContext analysis, ConcurrentBag<MonitorRule> monitors)
    {
        var monitor = InterceptorsGenerator.TryCreateMonitorRule(analysis.SemanticModel, (MethodDeclarationSyntax)analysis.Node, analysis.CancellationToken);
        if (monitor is not null)
            monitors.Add(monitor);
    }

    private static void Analyze(ImmutableArray<Rule> rules, ImmutableArray<MonitorRule> monitors, ImmutableArray<Invocation> calls, bool hasMonitoringRuntime, Action<Diagnostic> report)
    {
        var valid = InterceptorsGenerator.ValidateRules(rules, report);
        foreach (var monitor in monitors)
        {
            if (!InterceptorsGenerator.IsValidMonitorScope(monitor.Scope))
                report(Diagnostic.Create(Diagnostics.InvalidMonitorScope, monitor.DiagnosticLocation, monitor.Target.ToDisplayString()));
            if (monitor.Target.ReturnsByRef || monitor.Target.ReturnsByRefReadonly)
                report(Diagnostic.Create(Diagnostics.UnsupportedMonitorTarget, monitor.DiagnosticLocation, monitor.Target.ToDisplayString()));
        }
        foreach (var call in calls)
        {
            if (valid.Any(rule => SymbolEqualityComparer.Default.Equals(rule.Handler, call.ContainingMethod)))
                continue;
            if (call.MarkerId is { } marker)
            {
                var rule = valid.FirstOrDefault(candidate => string.Equals(candidate.Id, marker, StringComparison.Ordinal));
                if (rule is null)
                    report(Diagnostic.Create(Diagnostics.UnknownMarker, call.DiagnosticLocation, marker));
                else if ((rule.Scope & InterceptorsGenerator.Explicit) == 0)
                    report(Diagnostic.Create(Diagnostics.MarkerNotExplicit, call.DiagnosticLocation, marker));
                else if (rule.Target is null || !SymbolEqualityComparer.Default.Equals(rule.Target.OriginalDefinition, call.Target.OriginalDefinition))
                    report(Diagnostic.Create(Diagnostics.MarkerTargetMismatch, call.DiagnosticLocation, marker, call.Target.ToDisplayString()));
                continue;
            }

            var matching = valid.Where(rule => rule.Target is not null &&
                (rule.Scope & InterceptorsGenerator.Compilation) != 0 &&
                SymbolEqualityComparer.Default.Equals(rule.Target.OriginalDefinition, call.Target.OriginalDefinition) &&
                (!call.IsGenerated || (rule.Scope & InterceptorsGenerator.GeneratedCode) != 0)).ToArray();
            if (matching.Length > 1)
                report(Diagnostic.Create(Diagnostics.ConflictingCompilationRules, call.DiagnosticLocation, call.Target.ToDisplayString()));

            var monitor = InterceptorsGenerator.FindMonitorRule(call.Target, call.DiagnosticLocation, monitors);
            if (monitor is not null && call.IsGenerated && (monitor.Scope & InterceptorsGenerator.GeneratedCode) == 0)
                monitor = null;
            if (matching.Length == 1 && monitor is not null)
                report(Diagnostic.Create(Diagnostics.ConflictingMonitorRule, call.DiagnosticLocation, call.Target.ToDisplayString()));
            else if (monitor is not null && !hasMonitoringRuntime)
                report(Diagnostic.Create(Diagnostics.MissingMonitoringRuntime, call.DiagnosticLocation, call.Target.ToDisplayString()));
        }
    }
}
