using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ling.Interceptors;

[Generator(LanguageNames.CSharp)]
public sealed class InterceptorsGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Ling.Interceptors.InterceptAttribute";
    internal const int Explicit = 1;
    internal const int Compilation = 2;
    internal const int GeneratedCode = 4;
    private static readonly Regex IdRegex = new Regex("^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkerRegex = new Regex(@"^/\\*\\s*intercept\\s*:\\s*([A-Za-z][A-Za-z0-9_.-]*)\\s*\\*/$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static post => post.AddSource("LingInterceptors.Attributes.g.cs", SourceText.From(AttributesSource, Encoding.UTF8)));

        var rules = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, ct) => TryCreateRule(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var calls = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (ctx, ct) => TryCreateInvocation(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(context.CompilationProvider.Combine(rules.Collect()).Combine(calls.Collect()), Execute);
    }

    private static void Execute(SourceProductionContext context, ((Compilation Left, ImmutableArray<Rule> Right) Left, ImmutableArray<Invocation> Right) data)
    {
        var compilation = data.Left.Left;
        var rules = data.Left.Right;
        var calls = data.Right;

        var validRules = ValidateRules(rules, static _ => { });
        if (validRules.Length == 0)
            return;

        if (validRules.Any(static r => (r.Scope & Compilation) != 0 && (r.Scope & GeneratedCode) != 0))
            context.AddSource("LingInterceptors.TwoPhaseRequired.g.cs", SourceText.From("// Ling.Interceptors two-phase build marker\n", Encoding.UTF8));

        var selected = new Dictionary<Rule, List<Invocation>>(RuleComparer.Instance);
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

            if (matching.Length > 1)
            {
                continue;
            }
            if (matching.Length == 1)
                Add(selected, matching[0], invocation);
        }

        foreach (var pair in selected)
            context.AddSource("Interceptor_" + StableHint(pair.Key.Handler) + ".g.cs", SourceText.From(GenerateAdapter(pair.Key, pair.Value), Encoding.UTF8));
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
                .Where(static m => m.MethodKind == MethodKind.Ordinary)
                .Where(m => HasCompatibleSignature(rule.Handler, m))
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

    private static bool IsAccessibleFromGeneratedAdapter(IMethodSymbol handler) =>
        handler.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal &&
        handler.ContainingType.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;

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
        if (adjacent.Success)
            return adjacent.Groups[1].Value;
        return null;
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
            selected.Add(rule, values = new List<Invocation>());
        values.Add(invocation);
    }

    private static string GenerateAdapter(Rule rule, List<Invocation> locations)
    {
        var target = rule.Target!;
        var genericParameters = GetAllTypeParameters(target).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    { public InterceptsLocationAttribute(int version, string data) { } }");
        sb.AppendLine("}");
        sb.AppendLine("namespace Ling.Interceptors.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    file static class Interceptor");
        sb.AppendLine("    {");
        foreach (var location in locations)
            sb.Append("        ").AppendLine(location.Location.GetInterceptsLocationAttributeSyntax());

        var genericList = genericParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", genericParameters.Select(static p => p.Name)) + ">";
        var returnPrefix = target.ReturnsByRefReadonly ? "ref readonly " : target.ReturnsByRef ? "ref " : string.Empty;
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
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<ITypeParameterSymbol> GetAllTypeParameters(IMethodSymbol target)
    {
        var containers = new Stack<INamedTypeSymbol>();
        for (var current = target.ContainingType; current is not null; current = current.ContainingType)
            containers.Push(current);
        while (containers.Count != 0)
            foreach (var parameter in containers.Pop().TypeParameters)
                yield return parameter;
        foreach (var parameter in target.TypeParameters)
            yield return parameter;
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
        public static readonly RuleComparer Instance = new RuleComparer();
        public bool Equals(Rule? x, Rule? y) => x is not null && y is not null && SymbolEqualityComparer.Default.Equals(x.Handler, y.Handler);
        public int GetHashCode(Rule obj) => SymbolEqualityComparer.Default.GetHashCode(obj.Handler);
    }

    private const string AttributesSource = @"// <auto-generated/>
#nullable enable
namespace Ling.Interceptors
{
    [global::System.Flags]
    internal enum InterceptionScope { None = 0, Explicit = 1, Compilation = 2, GeneratedCode = 4 }

    [global::System.Diagnostics.Conditional(""LING_INTERCEPTORS_PRESERVE_ATTRIBUTES"")]
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class InterceptAttribute : global::System.Attribute
    {
        public InterceptAttribute(string id, string targetMethod, InterceptionScope scope)
        { }
    }
}";
}
