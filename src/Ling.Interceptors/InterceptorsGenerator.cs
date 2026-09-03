using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ling.Interceptors;

[Generator(LanguageNames.CSharp)]
public sealed class InterceptorsGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Ling.Interceptors.InterceptAttribute";
    private const string MonitorAttributeMetadataName = "Ling.Interceptors.MonitorAttribute";
    private const string SensitiveDataAttributeMetadataName = "Ling.Interceptors.SensitiveDataAttribute";
    internal const int Explicit = 1;
    internal const int Compilation = 2;
    internal const int GeneratedCode = 4;
    private static readonly Regex IdRegex = new("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkerRegex = new(@"^/\\*\\s*intercept\\s*:\\s*([A-Za-z][A-Za-z0-9_.-]*)\\s*\\*/$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rules = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => TryCreateRule(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var monitors = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => TryCreateMonitorRule(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var calls = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (ctx, ct) => TryCreateInvocation(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(
            context.CompilationProvider
                .Combine(rules.Collect())
                .Combine(monitors.Collect())
                .Combine(calls.Collect()),
            Execute);
    }

    private static void Execute(
        SourceProductionContext context,
        (((Compilation Left, ImmutableArray<Rule> Right) Left, ImmutableArray<MonitorRule> Right) Left, ImmutableArray<Invocation> Right) data)
    {
        var compilation = data.Left.Left.Left;
        var rules = data.Left.Left.Right;
        var monitors = data.Left.Right;
        var calls = data.Right;
        var validMonitors = monitors.Where(static monitor => IsValidMonitorScope(monitor.Scope)).ToImmutableArray();

        var validRules = ValidateRules(rules, static _ => { });
        if (validRules.Length == 0 && validMonitors.Length == 0 && !calls.Any(call => FindMonitorRule(call.Target, call.DiagnosticLocation, validMonitors) is not null))
            return;

        if (validRules.Any(static r => (r.Scope & Compilation) != 0 && (r.Scope & GeneratedCode) != 0)
            || validMonitors.Any(static r => (r.Scope & Compilation) != 0 && (r.Scope & GeneratedCode) != 0))
            context.AddSource("LingInterceptors.TwoPhaseRequired.g.cs", SourceText.From("// Ling.Interceptors two-phase build marker\r\n", Encoding.UTF8));

        var selected = new Dictionary<Rule, List<Invocation>>(RuleComparer.Instance);
        var selectedMonitors = new Dictionary<MonitorRule, List<Invocation>>(MonitorRuleComparer.Instance);
        foreach (var invocation in calls)
        {
            if (validRules.Any(r => SymbolEqualityComparer.Default.Equals(r.Handler, invocation.ContainingMethod)))
                continue;

            if (invocation.MarkerId is { } marker)
            {
                var markerRules = validRules.Where(r => string.Equals(r.Id, marker, StringComparison.Ordinal)).ToArray();
                if (markerRules.Length == 0)
                {
                    continue;
                }

                var rule = markerRules[0];
                if ((rule.Scope & Explicit) == 0)
                {
                    continue;
                }
                if (rule.Target is null || !SymbolEqualityComparer.Default.Equals(rule.Target.OriginalDefinition, invocation.Target.OriginalDefinition))
                {
                    continue;
                }
                Add(selected, rule, invocation);
                continue;
            }

            var matching = validRules.Where(r =>
                    (r.Scope & Compilation) != 0 &&
                    r.Target is not null &&
                    SymbolEqualityComparer.Default.Equals(r.Target.OriginalDefinition, invocation.Target.OriginalDefinition) &&
                    (!invocation.IsGenerated || (r.Scope & GeneratedCode) != 0))
                .ToArray();

            var monitor = FindMonitorRule(invocation.Target, invocation.DiagnosticLocation, validMonitors);
            if (monitor is not null && invocation.IsGenerated && (monitor.Scope & GeneratedCode) == 0)
                monitor = null;
            if (matching.Length > 1 || (matching.Length == 1 && monitor is not null))
            {
                continue;
            }
            if (monitor is not null)
            {
                Add(selectedMonitors, monitor, invocation);
                continue;
            }
            if (matching.Length == 1)
                Add(selected, matching[0], invocation);
        }

        foreach (var pair in selected)
            context.AddSource("Interceptor_" + StableHint(pair.Key.Handler) + ".g.cs", SourceText.From(GenerateAdapter(compilation, pair.Key, pair.Value), Encoding.UTF8));

        if (selectedMonitors.Count > 0 && compilation.GetTypeByMetadataName("Ling.Interceptors.MonitorRuntime") is null)
            return;

        foreach (var pair in selectedMonitors)
            context.AddSource("Monitor_" + StableHint(pair.Key.Target) + ".g.cs", SourceText.From(GenerateMonitorAdapter(compilation, pair.Key, pair.Value), Encoding.UTF8));
    }

    internal static ImmutableArray<Rule> ValidateRules(ImmutableArray<Rule> rules, Action<Diagnostic> report)
    {
        var builder = ImmutableArray.CreateBuilder<Rule>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            var valid = true;
            if (!IdRegex.IsMatch(rule.Id) || !ids.Add(rule.Id))
            {
                report(Diagnostic.Create(Diagnostics.InvalidId, rule.AttributeLocation, rule.Id));
                valid = false;
            }
            if ((rule.Scope & ~(Explicit | Compilation | GeneratedCode)) != 0 || ((rule.Scope & GeneratedCode) != 0 && (rule.Scope & Compilation) == 0))
            {
                report(Diagnostic.Create(Diagnostics.InvalidScope, rule.AttributeLocation));
                valid = false;
            }
            if (!rule.UsesTypeofAndNameof)
            {
                report(Diagnostic.Create(Diagnostics.InvalidAttributeArguments, rule.AttributeLocation));
                valid = false;
            }
            if (!rule.Handler.IsStatic || !IsAccessibleFromGeneratedAdapter(rule.Handler))
            {
                report(Diagnostic.Create(Diagnostics.InvalidHandler, rule.Handler.Locations.FirstOrDefault(), rule.Handler.ToDisplayString()));
                valid = false;
            }

            var candidates = rule.TargetType.GetMembers(rule.TargetName).OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary && HasCompatibleSignature(rule.Handler, m))
                .ToArray();
            if (candidates.Length != 1)
            {
                report(Diagnostic.Create(Diagnostics.TargetResolutionFailed, rule.AttributeLocation, rule.TargetType.ToDisplayString(), rule.TargetName));
                valid = false;
            }
            if (valid && rule.Scope != 0)
                builder.Add(rule.WithTarget(candidates[0]));
        }
        return builder.ToImmutable();
    }

    private static bool IsAccessibleFromGeneratedAdapter(IMethodSymbol handler)
        => handler.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            && handler.ContainingType.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;

    private static bool HasCompatibleSignature(IMethodSymbol handler, IMethodSymbol target)
    {
        if (handler.ReturnsByRef != target.ReturnsByRef || handler.ReturnsByRefReadonly != target.ReturnsByRefReadonly || !SymbolEqualityComparer.Default.Equals(handler.ReturnType, target.ReturnType))
            return false;

        var targetParams = target.Parameters;
        var handlerOffset = target.IsStatic ? 0 : 1;
        if (handler.Parameters.Length != targetParams.Length + handlerOffset)
            return false;
        if (!target.IsStatic && !SymbolEqualityComparer.Default.Equals(handler.Parameters[0].Type, target.ContainingType))
            return false;
        for (var i = 0; i < targetParams.Length; i++)
        {
            var actual = handler.Parameters[i + handlerOffset];
            var expected = targetParams[i];
            if (actual.RefKind != expected.RefKind || !SymbolEqualityComparer.Default.Equals(actual.Type, expected.Type))
                return false;
        }
        return true;
    }

    private static Rule? TryCreateRule(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol handler)
            return null;
        var attribute = handler.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttributeMetadataName);
        if (attribute is null || attribute.ConstructorArguments.Length != 3 || attribute.ConstructorArguments[0].Value is not string id || attribute.ConstructorArguments[1].Value is not string targetName || attribute.ConstructorArguments[2].Value is not int scope)
            return null;

        var syntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;
        var positionalArgs = syntax?.ArgumentList?.Arguments.Where(static a => a.NameEquals is null && a.NameColon is null).ToArray();
        var targetType = handler.ContainingType;
        var usesRequiredSyntax = false;
        if (positionalArgs is { Length: 3 } && positionalArgs[1].Expression is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" },
                ArgumentList.Arguments.Count: 1,
            } nameofExpression && nameofExpression.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var typeSymbol = context.SemanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol as INamedTypeSymbol;
            usesRequiredSyntax = typeSymbol is not null;
            if (typeSymbol is not null)
                targetType = typeSymbol;
        }
        return new Rule(id, targetType, targetName, scope, handler, null, syntax?.GetLocation() ?? method.GetLocation(), usesRequiredSyntax);
    }

    internal static Rule? TryCreateRule(SemanticModel semanticModel, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol handler)
            return null;
        var attribute = handler.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttributeMetadataName);
        if (attribute is null || attribute.ConstructorArguments.Length != 3 || attribute.ConstructorArguments[0].Value is not string id || attribute.ConstructorArguments[1].Value is not string targetName || attribute.ConstructorArguments[2].Value is not int scope)
            return null;
        var syntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;
        var positionalArgs = syntax?.ArgumentList?.Arguments.Where(static a => a.NameEquals is null && a.NameColon is null).ToArray();
        var targetType = handler.ContainingType;
        var usesRequiredSyntax = false;
        if (positionalArgs is { Length: 3 } && positionalArgs[1].Expression is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments.Count: 1 } nameofExpression && nameofExpression.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol is INamedTypeSymbol typeSymbol)
            {
                targetType = typeSymbol;
                usesRequiredSyntax = true;
            }
        }
        return new Rule(id, targetType, targetName, scope, handler, null, syntax?.GetLocation() ?? method.GetLocation(), usesRequiredSyntax);
    }

    private static MonitorRule? TryCreateMonitorRule(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)context.Node, cancellationToken) is not IMethodSymbol method)
            return null;

        return CreateMonitorRule(method, method.Locations.FirstOrDefault() ?? Location.None);
    }

    internal static MonitorRule? TryCreateMonitorRule(SemanticModel semanticModel, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol symbol)
            return null;

        return CreateMonitorRule(symbol, method.GetLocation());
    }

    internal static MonitorRule? FindMonitorRule(IMethodSymbol method, Location location, ImmutableArray<MonitorRule> declaredRules)
    {
        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
            return null;

        foreach (var rule in declaredRules)
        {
            if (SymbolEqualityComparer.Default.Equals(rule.Target.OriginalDefinition, method.OriginalDefinition))
                return rule;
        }

        var discovered = CreateMonitorRule(method, location);
        return discovered is not null && IsValidMonitorScope(discovered.Scope) ? discovered : null;
    }

    private static MonitorRule? CreateMonitorRule(IMethodSymbol method, Location location)
    {
        var attribute = method.GetAttributes().FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == MonitorAttributeMetadataName);
        if (attribute is null)
            return null;

        var scope = GetNamedInt(attribute, "Scope", Compilation);

        return new MonitorRule(
            method,
            scope,
            GetNamedBool(attribute, "CaptureParameters", false),
            GetNamedBool(attribute, "CaptureReturnValue", false),
            GetNamedBool(attribute, "CaptureExceptions", true),
            GetNamedBool(attribute, "RecordTiming", true),
            GetNamedBool(attribute, "CreateTrace", false),
            location);
    }

    private static bool GetNamedBool(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is bool value)
                return value;
        }

        return defaultValue;
    }

    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is int value)
                return value;
        }

        return defaultValue;
    }

    internal static bool IsValidMonitorScope(int scope)
        => (scope & ~(Compilation | GeneratedCode)) == 0 && (scope & Compilation) != 0;

    private static Invocation? TryCreateInvocation(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var syntax = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(syntax, cancellationToken) is not Microsoft.CodeAnalysis.Operations.IInvocationOperation operation)
            return null;
        if (operation.TargetMethod.MethodKind != MethodKind.Ordinary)
            return null;
        var interceptable = context.SemanticModel.GetInterceptableLocation(syntax, cancellationToken);
        if (interceptable is null)
            return null;
        var name = GetInvokedName(syntax.Expression);
        var marker = name is null ? null : GetMarker(name);
        var containingMethod = context.SemanticModel.GetEnclosingSymbol(syntax.SpanStart, cancellationToken) as IMethodSymbol;
        return new Invocation(operation.TargetMethod, interceptable, syntax.GetLocation(), marker, IsGenerated(syntax.SyntaxTree, syntax), containingMethod);
    }

    internal static Invocation? TryCreateInvocation(SemanticModel semanticModel, InvocationExpressionSyntax syntax, CancellationToken cancellationToken)
    {
        if (semanticModel.GetOperation(syntax, cancellationToken) is not Microsoft.CodeAnalysis.Operations.IInvocationOperation operation || operation.TargetMethod.MethodKind != MethodKind.Ordinary)
            return null;
        var interceptable = semanticModel.GetInterceptableLocation(syntax, cancellationToken);
        if (interceptable is null)
            return null;
        var name = GetInvokedName(syntax.Expression);
        var marker = name is null ? null : GetMarker(name);
        return new Invocation(operation.TargetMethod, interceptable, syntax.GetLocation(), marker, IsGenerated(syntax.SyntaxTree, syntax), semanticModel.GetEnclosingSymbol(syntax.SpanStart, cancellationToken) as IMethodSymbol);
    }

    private static SimpleNameSyntax? GetInvokedName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name,
        MemberBindingExpressionSyntax member => member.Name,
        SimpleNameSyntax simple => simple,
        _ => null,
    };

    private static string? GetMarker(SimpleNameSyntax name)
    {
        var triviaToCheck = name.GetLeadingTrivia().ToList();
        if (name.Parent is MemberAccessExpressionSyntax member)
            triviaToCheck.AddRange(member.OperatorToken.TrailingTrivia);
        else if (name.Parent is MemberBindingExpressionSyntax binding)
            triviaToCheck.AddRange(binding.OperatorToken.TrailingTrivia);
        foreach (var trivia in triviaToCheck)
        {
            if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                continue;
            var match = MarkerRegex.Match(trivia.ToFullString().Trim());
            if (match.Success)
                return match.Groups[1].Value;
        }
        // Trivia between '.' and an identifier is attached differently across syntax forms.
        // Inspect only the immediate text preceding this simple name, so nested invocations remain unambiguous.
        var start = Math.Max(0, name.SpanStart - 128);
        var preceding = name.SyntaxTree.GetText().ToString(TextSpan.FromBounds(start, name.SpanStart));
        var adjacent = Regex.Match(preceding, @"/\*\s*intercept\s*:\s*([A-Za-z][A-Za-z0-9_.-]*)\s*\*/\s*$", RegexOptions.CultureInvariant);
        return adjacent.Success ? adjacent.Groups[1].Value : null;
    }

    private static bool IsGenerated(SyntaxTree tree, SyntaxNode node)
    {
        var path = tree.FilePath ?? string.Empty;
        if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            return true;
        var text = tree.GetText();
        if (text.ToString(new TextSpan(0, Math.Min(text.Length, 256))).IndexOf("<auto-generated", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            if (current is MemberDeclarationSyntax member && member.AttributeLists.ToString().IndexOf("GeneratedCode", StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static void Add(Dictionary<Rule, List<Invocation>> selected, Rule rule, Invocation invocation)
    {
        if (!selected.TryGetValue(rule, out var values))
            selected.Add(rule, values = []);
        values.Add(invocation);
    }

    private static void Add(Dictionary<MonitorRule, List<Invocation>> selected, MonitorRule rule, Invocation invocation)
    {
        if (!selected.TryGetValue(rule, out var values))
            selected.Add(rule, values = []);
        values.Add(invocation);
    }

    private static string GenerateAdapter(Compilation compilation, Rule rule, List<Invocation> locations)
    {
        var target = rule.Target!;
        var genericParameters = GetAllTypeParameters(target).ToArray();
        var sourceRoot = GetSourceRoot(compilation);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file was generated by Ling.Interceptors. Do not edit it manually.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Provides the compiler interception location metadata.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.Append("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ling.Interceptors\", \"").Append(GeneratorVersion).AppendLine("\")]");
        sb.AppendLine("    [global::System.Diagnostics.DebuggerNonUserCode]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes interception location metadata.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"version\">The compiler interception metadata version.</param>");
        sb.AppendLine("        /// <param name=\"data\">The encoded interception location.</param>");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data)");
        sb.AppendLine("        {");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("namespace Ling.Interceptors.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Provides the generated interception adapter for the configured handler.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ling.Interceptors\", \"").Append(GeneratorVersion).AppendLine("\")]");
        sb.AppendLine("    [global::System.Diagnostics.DebuggerNonUserCode]");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
        sb.AppendLine("    file static class Interceptor");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Dispatches the intercepted call to the user-provided handler.");
        sb.AppendLine("        /// </summary>");
        var genericList = genericParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", genericParameters.Select(static p => p.Name)) + ">";
        var returnPrefix = target.ReturnsByRefReadonly ? "ref readonly " : target.ReturnsByRef ? "ref " : string.Empty;
        AppendParameterDocumentation(sb, target);
        foreach (var location in locations)
        {
            sb.Append("        ").Append(location.Location.GetInterceptsLocationAttributeSyntax());
            sb.Append(" // ").AppendLine(FormatSourceLocation(location.DiagnosticLocation, sourceRoot));
        }
        sb.Append("        internal static ").Append(returnPrefix).Append(TypeName(target.ReturnType)).Append(" __Intercept").Append(genericList).Append('(');
        var arguments = new List<string>();
        if (!target.IsStatic)
        {
            sb.Append("this ").Append(TypeName(target.ContainingType)).Append(" receiver");
            arguments.Add("receiver");
        }
        for (var i = 0; i < target.Parameters.Length; i++)
        {
            if (!target.IsStatic || i != 0)
                sb.Append(arguments.Count == 0 ? string.Empty : ", ");
            var parameter = target.Parameters[i];
            sb.Append(ParameterDeclaration(parameter, "p" + i));
            arguments.Add(Argument(parameter, "p" + i));
        }
        sb.AppendLine(")");
        foreach (var parameter in genericParameters)
            AppendConstraints(sb, parameter);
        sb.AppendLine("        {");
        var handlerCall = HandlerCall(rule.Handler, genericParameters, arguments);
        if (target.ReturnsByRef || target.ReturnsByRefReadonly)
            sb.Append("            return ref ").Append(handlerCall).AppendLine(";");
        else if (target.ReturnsVoid)
            sb.Append("            ").Append(handlerCall).AppendLine(";");
        else
            sb.Append("            return ").Append(handlerCall).AppendLine(";");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return NormalizeGeneratedLineEndings(sb.ToString());
    }

    private static string GenerateMonitorAdapter(Compilation compilation, MonitorRule rule, List<Invocation> locations)
    {
        var target = rule.Target;
        var genericParameters = GetAllTypeParameters(target).ToArray();
        var sourceRoot = GetSourceRoot(compilation);
        var category = target.ContainingType.ToDisplayString();
        var methodName = target.Name;
        var isTask = IsTaskLike(target.ReturnType, out var taskResultType, out var taskKind);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file was generated by Ling.Interceptors. Do not edit it manually.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        AppendInterceptsLocationAttribute(sb);
        sb.AppendLine();
        sb.AppendLine("namespace Ling.Interceptors.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ling.Interceptors\", \"" + GeneratorVersion + "\")]");
        sb.AppendLine("    [global::System.Diagnostics.DebuggerNonUserCode]");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.CompilerGenerated]");
        sb.AppendLine("    file static class MonitorInterceptor");
        sb.AppendLine("    {");
        foreach (var location in locations)
        {
            sb.Append("        ").Append(location.Location.GetInterceptsLocationAttributeSyntax());
            sb.Append(" // ").AppendLine(FormatSourceLocation(location.DiagnosticLocation, sourceRoot));
        }

        var returnPrefix = target.ReturnsByRefReadonly ? "ref readonly " : target.ReturnsByRef ? "ref " : string.Empty;
        var asyncPrefix = isTask ? "async " : string.Empty;
        var genericList = genericParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", genericParameters.Select(static parameter => parameter.Name)) + ">";
        sb.Append("        internal static ").Append(asyncPrefix).Append(returnPrefix).Append(TypeName(target.ReturnType)).Append(" __Monitor").Append(genericList).Append('(');
        var arguments = new List<string>();
        if (!target.IsStatic)
        {
            sb.Append("this ").Append(TypeName(target.ContainingType)).Append(" receiver");
            arguments.Add("receiver");
        }

        for (var index = 0; index < target.Parameters.Length; index++)
        {
            if (arguments.Count > 0)
                sb.Append(", ");
            var parameter = target.Parameters[index];
            sb.Append(ParameterDeclaration(parameter, "p" + index));
            arguments.Add(Argument(parameter, "p" + index));
        }

        sb.AppendLine(")");
        foreach (var parameter in genericParameters)
            AppendConstraints(sb, parameter);
        sb.AppendLine("        {");
        sb.AppendLine("            var __parameters = new global::System.Collections.Generic.Dictionary<string, global::Ling.Interceptors.MonitorValue>();");
        if (rule.CaptureParameters)
        {
            for (var index = 0; index < target.Parameters.Length; index++)
            {
                var parameter = target.Parameters[index];
                var sensitive = HasSensitiveData(parameter.GetAttributes());
                sb.Append("            __parameters.Add(\"").Append(EscapeString(parameter.Name)).Append("\", global::Ling.Interceptors.MonitorRuntime.Format(p").Append(index)
                    .Append(", new global::Ling.Interceptors.MonitorValueContext(\"").Append(EscapeString(parameter.Name)).Append("\", typeof(").Append(TypeName(parameter.Type)).Append("), ").Append(sensitive ? "true" : "false").AppendLine("))); ");
            }
        }

        sb.Append("            var __operation = global::Ling.Interceptors.MonitorRuntime.Begin(new global::Ling.Interceptors.MonitorStartContext(\"").Append(EscapeString(category)).Append("\", \"").Append(EscapeString(methodName)).Append("\", __parameters, ").Append(rule.CreateTrace ? "true" : "false").AppendLine("));");
        sb.AppendLine("            var __stopwatch = global::System.Diagnostics.Stopwatch.StartNew();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        var call = TargetCall(target, arguments);
        if (target.ReturnsVoid)
        {
            sb.Append("                ").Append(call).AppendLine(";");
            AppendCompletion(sb, rule, category, methodName, "null");
        }
        else if (isTask)
        {
            if (taskKind == TaskKind.TaskWithoutResult || taskKind == TaskKind.ValueTaskWithoutResult)
            {
                sb.Append("                await ").Append(call).AppendLine(".ConfigureAwait(false);");
                AppendCompletion(sb, rule, category, methodName, "null");
            }
            else
            {
                sb.Append("                var __result = await ").Append(call).AppendLine(".ConfigureAwait(false);");
                var sensitive = HasSensitiveData(target.GetReturnTypeAttributes());
                var returnValue = rule.CaptureReturnValue
                    ? "global::Ling.Interceptors.MonitorRuntime.Format(__result, new global::Ling.Interceptors.MonitorValueContext(\"return\", typeof(" + TypeName(taskResultType!) + "), " + (sensitive ? "true" : "false") + "))"
                    : "null";
                AppendCompletion(sb, rule, category, methodName, returnValue);
                sb.AppendLine("                return __result;");
            }
        }
        else
        {
            sb.Append("                var __result = ").Append(call).AppendLine(";");
            var sensitive = HasSensitiveData(target.GetReturnTypeAttributes());
            var returnValue = rule.CaptureReturnValue
                ? "global::Ling.Interceptors.MonitorRuntime.Format(__result, new global::Ling.Interceptors.MonitorValueContext(\"return\", typeof(" + TypeName(target.ReturnType) + "), " + (sensitive ? "true" : "false") + "))"
                : "null";
            AppendCompletion(sb, rule, category, methodName, returnValue);
            sb.AppendLine("                return __result;");
        }
        sb.AppendLine("            }");
        sb.AppendLine("            catch (global::System.Exception __exception)");
        sb.AppendLine("            {");
        sb.AppendLine("                __stopwatch.Stop();");
        if (rule.CaptureExceptions)
        {
            sb.Append("                __operation.Fail(new global::Ling.Interceptors.MonitorCompletionContext(\"").Append(EscapeString(category)).Append("\", \"").Append(EscapeString(methodName)).Append("\", __stopwatch.Elapsed, ").Append(rule.RecordTiming ? "true" : "false").AppendLine("), __exception);");
        }
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                __operation.Dispose();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return NormalizeGeneratedLineEndings(sb.ToString());
    }

    private static string NormalizeGeneratedLineEndings(string value)
        => value.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static void AppendCompletion(StringBuilder sb, MonitorRule rule, string category, string methodName, string returnValue)
    {
        sb.AppendLine("                __stopwatch.Stop();");
        sb.Append("                __operation.Complete(new global::Ling.Interceptors.MonitorCompletionContext(\"").Append(EscapeString(category)).Append("\", \"").Append(EscapeString(methodName)).Append("\", __stopwatch.Elapsed, ").Append(rule.RecordTiming ? "true" : "false").Append("), ").Append(returnValue).AppendLine(");");
    }

    private static void AppendInterceptsLocationAttribute(StringBuilder sb)
    {
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static string TargetCall(IMethodSymbol target, List<string> arguments)
    {
        var values = target.IsStatic ? arguments : arguments.Skip(1).ToList();
        var generic = target.Arity == 0 ? string.Empty : "<" + string.Join(", ", target.TypeParameters.Select(static parameter => parameter.Name)) + ">";
        var receiver = target.IsStatic ? TypeName(target.ContainingType) : "receiver";
        return receiver + "." + target.Name + generic + "(" + string.Join(", ", values) + ")";
    }

    private static bool HasSensitiveData(ImmutableArray<AttributeData> attributes)
        => attributes.Any(static attribute => attribute.AttributeClass?.ToDisplayString() == SensitiveDataAttributeMetadataName);

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private enum TaskKind
    {
        TaskWithoutResult,
        TaskWithResult,
        ValueTaskWithoutResult,
        ValueTaskWithResult,
    }

    private static bool IsTaskLike(ITypeSymbol type, out ITypeSymbol? resultType, out TaskKind taskKind)
    {
        var display = type.OriginalDefinition.ToDisplayString();
        if (display == "System.Threading.Tasks.Task")
        {
            resultType = null;
            taskKind = TaskKind.TaskWithoutResult;
            return true;
        }
        if (display == "System.Threading.Tasks.Task<TResult>")
        {
            resultType = ((INamedTypeSymbol)type).TypeArguments[0];
            taskKind = TaskKind.TaskWithResult;
            return true;
        }
        if (display == "System.Threading.Tasks.ValueTask")
        {
            resultType = null;
            taskKind = TaskKind.ValueTaskWithoutResult;
            return true;
        }
        if (display == "System.Threading.Tasks.ValueTask<TResult>")
        {
            resultType = ((INamedTypeSymbol)type).TypeArguments[0];
            taskKind = TaskKind.ValueTaskWithResult;
            return true;
        }

        resultType = null;
        taskKind = default;
        return false;
    }

    private static IEnumerable<ITypeParameterSymbol> GetAllTypeParameters(IMethodSymbol target)
    {
        var containers = new Stack<INamedTypeSymbol>();
        for (var current = target.ContainingType; current is not null; current = current.ContainingType)
            containers.Push(current);
        while (containers.Count != 0)
        {
            foreach (var parameter in containers.Pop().TypeParameters)
            {
                yield return parameter;
            }
        }

        foreach (var parameter in target.TypeParameters)
        {
            yield return parameter;
        }
    }

    private static string HandlerCall(IMethodSymbol handler, IEnumerable<ITypeParameterSymbol> genericParameters, List<string> arguments)
    {
        var containing = handler.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var generic = handler.Arity == 0 ? string.Empty : "<" + string.Join(", ", genericParameters.Take(handler.Arity).Select(static p => p.Name)) + ">";
        return containing + "." + handler.Name + generic + "(" + string.Join(", ", arguments) + ")";
    }

    private static string ParameterDeclaration(IParameterSymbol parameter, string name)
    {
        var modifier = parameter.RefKind switch { RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ", _ => string.Empty };
        return modifier + TypeName(parameter.Type) + " " + name;
    }

    private static string Argument(IParameterSymbol parameter, string name) => parameter.RefKind switch
    {
        RefKind.Ref => "ref " + name,
        RefKind.Out => "out " + name,
        RefKind.In => "in " + name,
        _ => name,
    };

    private static string TypeName(ITypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static void AppendParameterDocumentation(StringBuilder sb, IMethodSymbol target)
    {
        if (!target.IsStatic)
            sb.AppendLine("        /// <param name=\"receiver\">The instance on which the intercepted method was invoked.</param>");

        for (var i = 0; i < target.Parameters.Length; i++)
        {
            var description = target.Parameters[i].RefKind == RefKind.Out
                ? "The output value produced by the intercepted method."
                : "The value passed to the intercepted method.";
            sb.Append("        /// <param name=\"p").Append(i).Append("\">").Append(description).AppendLine("</param>");
        }
    }

    private static string FormatSourceLocation(Location location, string? sourceRoot)
    {
        var lineSpan = location.GetLineSpan();
        var sourcePath = string.IsNullOrEmpty(lineSpan.Path) ? location.SourceTree?.FilePath : lineSpan.Path;
        var path = string.IsNullOrEmpty(sourcePath)
            ? "unknown file"
            : GetRelativePath(sourcePath!, sourceRoot);
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;
        return $"{path} ({line}, {column})".Replace("*/", "* /");
    }

    private static string? GetSourceRoot(Compilation compilation)
    {
        var directories = compilation.SyntaxTrees
            .Select(static tree => tree.FilePath)
            .Where(static path => !string.IsNullOrEmpty(path))
            .Select(static path => Path.GetDirectoryName(Path.GetFullPath(path!)))
            .Where(static path => !string.IsNullOrEmpty(path))
            .Select(static path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (directories.Length == 0)
            return null;

        var root = directories[0];
        foreach (var directory in directories.Skip(1))
        {
            while (!IsSameOrDescendant(directory, root))
            {
                var parent = GetParentPath(root);
                if (parent is null || string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
                    return null;
                root = parent;
            }
        }
        return root;
    }

    private static string? GetParentPath(string path)
    {
        var separator = path.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return separator <= 0 ? null : path.Substring(0, separator);
    }

    private static string GetRelativePath(string path, string? sourceRoot)
    {
        if (sourceRoot is null)
            return path.Replace('\\', '/');

        var fullPath = Path.GetFullPath(path);
        if (!IsSameOrDescendant(fullPath, sourceRoot))
            return fullPath.Replace('\\', '/');

        var relative = fullPath.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return (relative.Length == 0 ? Path.GetFileName(fullPath) : relative).Replace('\\', '/');
    }

    private static bool IsSameOrDescendant(string path, string directory)
    {
        if (string.Equals(path, directory, StringComparison.OrdinalIgnoreCase))
            return true;
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendConstraints(StringBuilder sb, ITypeParameterSymbol parameter)
    {
        var parts = new List<string>();
        if (parameter.HasReferenceTypeConstraint) parts.Add("class");
        else if (parameter.HasValueTypeConstraint) parts.Add("struct");
        else if (parameter.HasUnmanagedTypeConstraint) parts.Add("unmanaged");
        parts.AddRange(parameter.ConstraintTypes.Select(TypeName));
        if (parameter.HasConstructorConstraint) parts.Add("new()");
        if (parts.Count > 0)
            sb.Append("            where ").Append(parameter.Name).Append(" : ").Append(string.Join(", ", parts)).AppendLine();
    }

    private static string StableHint(IMethodSymbol method)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) hash = hash * 31 + c;
            return ((uint)hash).ToString("X8");
        }
    }

    private sealed class RuleComparer : IEqualityComparer<Rule>
    {
        public static readonly RuleComparer Instance = new();
        public bool Equals(Rule? x, Rule? y) => x is not null && y is not null && SymbolEqualityComparer.Default.Equals(x.Handler, y.Handler);
        public int GetHashCode(Rule obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Handler);
    }

    private sealed class MonitorRuleComparer : IEqualityComparer<MonitorRule>
    {
        public static readonly MonitorRuleComparer Instance = new();

        public bool Equals(MonitorRule? x, MonitorRule? y)
            => x is not null && y is not null && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);

        public int GetHashCode(MonitorRule obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Target);
    }

    private static string GeneratorVersion
        => typeof(InterceptorsGenerator).Assembly.GetName().Version?.ToString() ?? "1.1.0.0";
}
