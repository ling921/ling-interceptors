using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
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
            var calls = new ConcurrentBag<Invocation>();
            start.RegisterSyntaxNodeAction(analysis => AddRule(analysis, rules), SyntaxKind.MethodDeclaration);
            start.RegisterSyntaxNodeAction(analysis => AddCall(analysis, calls), SyntaxKind.InvocationExpression);
            start.RegisterCompilationEndAction(analysis => Analyze(rules.ToImmutableArray(), calls.ToImmutableArray(), analysis.ReportDiagnostic));
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

    private static void Analyze(ImmutableArray<Rule> rules, ImmutableArray<Invocation> calls, Action<Diagnostic> report)
    {
        var valid = InterceptorsGenerator.ValidateRules(rules, report);
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
        }
    }
}
