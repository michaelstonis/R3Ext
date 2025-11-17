using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.ComponentModel;

namespace R3Ext.Bindings.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class BindingGeneratorV2 : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax ies && LooksLikeBindingInvocation(ies),
                static (ctx, _) => Transform(ctx))
            .Where(m => m is not null)!;

        var collected = invocations.Collect();
        var assemblyName = context.CompilationProvider.Select((c, _) => c.AssemblyName ?? string.Empty);
        var combined = collected.Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var models = tuple.Left;
            var asmName = tuple.Right;
            var all = models!.OfType<InvocationModel>().ToImmutableArray();
            var emitter = new CodeEmitter();
            var source = emitter.Emit(all, asmName);
            spc.AddSource("R3Ext_BindingGeneratorV2.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static bool LooksLikeBindingInvocation(InvocationExpressionSyntax ies)
    {
        if (ies.Expression is MemberAccessExpressionSyntax maes)
        {
            var name = maes.Name.Identifier.ValueText;
            return name is "BindTwoWay" or "BindOneWay" or "WhenChanged";
        }
        return false;
    }

    private static InvocationModel? Transform(GeneratorSyntaxContext ctx)
    {
        var ies = (InvocationExpressionSyntax)ctx.Node;
        if (ies.Expression is not MemberAccessExpressionSyntax maes) return null;
        var name = maes.Name.Identifier.ValueText;

        // Use invocation expression for symbol resolution to properly catch extension methods in other assemblies.
        var si = ctx.SemanticModel.GetSymbolInfo(ies);
        var symbol = si.Symbol as IMethodSymbol ?? si.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (symbol is null) return null;
        if (symbol.Name is not ("BindTwoWay" or "BindOneWay" or "WhenChanged")) return null;

        var args = ies.ArgumentList.Arguments;
        string? fromPath = null, toPath = null, whenPath = null;
        SimpleLambdaExpressionSyntax? fromLambda = null, toLambda = null, whenLambda = null;
        var fromSegments = new List<PropertySegment>();
        var toSegments = new List<PropertySegment>();

        string? inferredTargetRootTypeNameFromHeuristic = null;
        if (symbol.Name == "WhenChanged")
        {
            if (args.Count < 1) return null;
            whenLambda = args[0].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[0].Expression) as SimpleLambdaExpressionSyntax;
            whenPath = whenLambda?.ToString();
            if (whenLambda is not null) ExtractSegments(ctx.SemanticModel, whenLambda, fromSegments); // reuse fromSegments for WhenChanged chain
        }
        else
        {
            // BindOneWay / BindTwoWay minimal required args: targetObject, fromProperty, toProperty
            if (args.Count < 3) return null;
            fromLambda = args[1].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[1].Expression) as SimpleLambdaExpressionSyntax;
            toLambda = args[2].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[2].Expression) as SimpleLambdaExpressionSyntax;
            fromPath = fromLambda?.ToString();
            toPath = toLambda?.ToString();
            if (fromLambda is not null) ExtractSegments(ctx.SemanticModel, fromLambda, fromSegments);
            if (toLambda is not null)
            {
                ITypeSymbol? targetRootType = null;
                if (symbol.IsGenericMethod && symbol.TypeArguments.Length >= 3)
                {
                    var ta = symbol.TypeArguments[2];
                    if (ta is not null && ta.TypeKind != TypeKind.TypeParameter) targetRootType = ta;
                }
                var convType = ctx.SemanticModel.GetTypeInfo(toLambda).ConvertedType as INamedTypeSymbol;
                if (convType is not null)
                {
                    if (convType.Name == "Expression" && convType.TypeArguments.Length == 1 && convType.TypeArguments[0] is INamedTypeSymbol inner && inner.Name == "Func" && inner.TypeArguments.Length >= 1)
                    {
                        targetRootType = inner.TypeArguments[0];
                    }
                    else if (convType.Name == "Func" && convType.TypeArguments.Length >= 1)
                    {
                        targetRootType = convType.TypeArguments[0];
                    }
                }
                targetRootType ??= ctx.SemanticModel.GetTypeInfo(args[0].Expression).Type;
                if (targetRootType is null && args[0].Expression is IdentifierNameSyntax idName)
                {
                    var idText = idName.Identifier.ValueText;
                    var comp = ctx.SemanticModel.Compilation;
                    static ITypeSymbol? GetType(Compilation c, string full) => c.GetTypeByMetadataName(full);
                    if (idText.EndsWith("Entry", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Entry") ?? GetType(comp, "Microsoft.Maui.Controls.InputView");
                    else if (idText.EndsWith("Label", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Label");
                    else if (idText.EndsWith("Editor", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Editor") ?? GetType(comp, "Microsoft.Maui.Controls.InputView");
                    else if (idText.EndsWith("Switch", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Switch");
                    else if (idText.EndsWith("Slider", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Slider");
                    else if (idText.EndsWith("Stepper", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.Stepper");
                    else if (idText.EndsWith("DatePicker", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.DatePicker");
                    else if (idText.EndsWith("TimePicker", StringComparison.Ordinal)) targetRootType = GetType(comp, "Microsoft.Maui.Controls.TimePicker");
                }
                ExtractSegments(ctx.SemanticModel, toLambda, toSegments, targetRootType);

                // Fallback: if still unresolved, synthesize segment using MAUI name + property heuristics
                if (toSegments.Count == 0 && args[0].Expression is IdentifierNameSyntax id2)
                {
                    var varName = id2.Identifier.ValueText;
                    // extract last member name from lambda (e.g., Text)
                    static string? LastMemberName(ExpressionSyntax? ex)
                    {
                        while (ex is PostfixUnaryExpressionSyntax px && px.OperatorToken.IsKind(SyntaxKind.ExclamationToken)) ex = px.Operand;
                        if (ex is MemberAccessExpressionSyntax m)
                        {
                            return m.Name.Identifier.ValueText;
                        }
                        return null;
                    }
                    var leaf = LastMemberName(toLambda.Body as ExpressionSyntax);
                    string? rootTypeName = null;
                    string? leafTypeName = null;
                    if (leaf is not null)
                    {
                        if (varName.EndsWith("Entry", StringComparison.Ordinal) || varName.EndsWith("Editor", StringComparison.Ordinal))
                        {
                            if (leaf == "Text") { rootTypeName = "global::Microsoft.Maui.Controls.InputView"; leafTypeName = "global::System.String"; }
                        }
                        else if (varName.EndsWith("Label", StringComparison.Ordinal))
                        {
                            if (leaf == "Text") { rootTypeName = "global::Microsoft.Maui.Controls.Label"; leafTypeName = "global::System.String"; }
                        }
                        else if (varName.EndsWith("Switch", StringComparison.Ordinal))
                        {
                            if (leaf == "IsToggled") { rootTypeName = "global::Microsoft.Maui.Controls.Switch"; leafTypeName = "global::System.Boolean"; }
                        }
                        else if (varName.EndsWith("Slider", StringComparison.Ordinal) || varName.EndsWith("Stepper", StringComparison.Ordinal))
                        {
                            if (leaf == "Value") { rootTypeName = varName.EndsWith("Slider", StringComparison.Ordinal) ? "global::Microsoft.Maui.Controls.Slider" : "global::Microsoft.Maui.Controls.Stepper"; leafTypeName = "global::System.Double"; }
                        }
                        else if (varName.EndsWith("DatePicker", StringComparison.Ordinal))
                        {
                            if (leaf == "Date") { rootTypeName = "global::Microsoft.Maui.Controls.DatePicker"; leafTypeName = "global::System.DateTime"; }
                        }
                        else if (varName.EndsWith("TimePicker", StringComparison.Ordinal))
                        {
                            if (leaf == "Time") { rootTypeName = "global::Microsoft.Maui.Controls.TimePicker"; leafTypeName = "global::System.TimeSpan"; }
                        }
                    }
                    if (rootTypeName is not null && leafTypeName is not null && leaf is not null)
                    {
                        inferredTargetRootTypeNameFromHeuristic = rootTypeName;
                        toSegments.Add(new PropertySegment
                        {
                            Name = leaf,
                            TypeName = leafTypeName,
                            DeclaringTypeName = rootTypeName,
                            IsReferenceType = leafTypeName is "global::System.String",
                            IsNotify = false,
                            HasSetter = true,
                            SetterIsNonPublic = false,
                            IsNonPublic = false
                        });
                    }
                }
            }
        }

        var model = new InvocationModel(symbol.Name, fromLambda, toLambda, whenLambda, fromPath, toPath, whenPath, ies.GetLocation(), ctx.SemanticModel.Compilation.AssemblyName ?? string.Empty)
        {
            FromSegments = fromSegments,
            ToSegments = toSegments
        };
        if (symbol.Name != "WhenChanged")
        {
            ITypeSymbol? ta = null;
            if (symbol.IsGenericMethod && symbol.TypeArguments.Length >= 3)
            {
                var targ = symbol.TypeArguments[2];
                if (targ is not null && targ.TypeKind != TypeKind.TypeParameter) ta = targ;
            }
            if (toLambda is not null)
            {
                var convType = ctx.SemanticModel.GetTypeInfo(toLambda).ConvertedType as INamedTypeSymbol;
                if (convType is not null)
                {
                    if (convType.Name == "Expression" && convType.TypeArguments.Length == 1 && convType.TypeArguments[0] is INamedTypeSymbol inner && inner.Name == "Func" && inner.TypeArguments.Length >= 1)
                    {
                        ta = inner.TypeArguments[0];
                    }
                    else if (convType.Name == "Func" && convType.TypeArguments.Length >= 1)
                    {
                        ta = convType.TypeArguments[0];
                    }
                }
            }
            ta ??= ctx.SemanticModel.GetTypeInfo(args[0].Expression).Type;
            model.TargetArgTypeName = ta?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? inferredTargetRootTypeNameFromHeuristic;
        }
        return model;
    }

    private static LambdaExpressionSyntax? TryExtractLambdaFromNameof(ExpressionSyntax expr) => expr as LambdaExpressionSyntax;

    private static void ExtractSegments(SemanticModel model, SimpleLambdaExpressionSyntax lambda, List<PropertySegment> into)
    {
        into.Clear();
        ExpressionSyntax? body = lambda.Body as ExpressionSyntax;
        // unwrap null-forgiving operators
        while (body is PostfixUnaryExpressionSyntax px and { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken })
        {
            body = px.Operand;
        }
        if (body is not MemberAccessExpressionSyntax) return;
        // Gather chain root->leaf
        var stack = new Stack<MemberAccessExpressionSyntax>();
        ExpressionSyntax? cur = body;
        while (cur is MemberAccessExpressionSyntax mae)
        {
            stack.Push(mae);
            cur = mae.Expression;
            while (cur is PostfixUnaryExpressionSyntax px2 && px2.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
            {
                cur = px2.Operand;
            }
        }
        if (cur is not IdentifierNameSyntax) return;
        while (stack.Count > 0)
        {
            var mae = stack.Pop();
            var sym = model.GetSymbolInfo(mae).Symbol as IPropertySymbol;
            if (sym is null) { into.Clear(); return; }
            var seg = new PropertySegment
            {
                Name = sym.Name,
                TypeName = sym.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DeclaringTypeName = sym.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsReferenceType = sym.Type.IsReferenceType,
                IsNotify = ImplementsNotify(sym.Type),
                HasSetter = sym.SetMethod is not null,
                SetterIsNonPublic = sym.SetMethod is not null && sym.SetMethod.DeclaredAccessibility != Accessibility.Public,
                IsNonPublic = sym.DeclaredAccessibility != Accessibility.Public
            };
            into.Add(seg);
        }
    }

    private static void ExtractSegments(SemanticModel model, SimpleLambdaExpressionSyntax lambda, List<PropertySegment> into, ITypeSymbol? explicitRoot)
    {
        into.Clear();
        ExpressionSyntax? body = lambda.Body as ExpressionSyntax;
        while (body is PostfixUnaryExpressionSyntax px && px.OperatorToken.IsKind(SyntaxKind.ExclamationToken)) body = px.Operand;
        if (body is not MemberAccessExpressionSyntax) return;
        var parts = new Stack<string>();
        ExpressionSyntax? cur = body;
        while (cur is MemberAccessExpressionSyntax mae)
        {
            parts.Push(mae.Name.Identifier.ValueText);
            cur = mae.Expression;
            while (cur is PostfixUnaryExpressionSyntax px2 && px2.OperatorToken.IsKind(SyntaxKind.ExclamationToken)) cur = px2.Operand;
        }
        if (explicitRoot is null) { ExtractSegments(model, lambda, into); return; }
        var current = explicitRoot;
        while (parts.Count > 0)
        {
            var name = parts.Pop();
            IPropertySymbol? prop = null;
            ITypeSymbol? search = current;
            while (search is not null && prop is null)
            {
                prop = search.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
                if (prop is null) search = search.BaseType;
            }
            if (prop is null)
            {
                // Fallback: try semantic model symbol resolution on the full member access
                if (body is MemberAccessExpressionSyntax mae)
                {
                    var sym = model.GetSymbolInfo(mae).Symbol as IPropertySymbol;
                    if (sym is null) { into.Clear(); return; }
                    prop = sym;
                }
                else { into.Clear(); return; }
            }
            var seg = new PropertySegment
            {
                Name = prop.Name,
                TypeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DeclaringTypeName = prop.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsReferenceType = prop.Type.IsReferenceType,
                IsNotify = ImplementsNotify(prop.Type),
                HasSetter = prop.SetMethod is not null,
                SetterIsNonPublic = prop.SetMethod is not null && prop.SetMethod.DeclaredAccessibility != Accessibility.Public,
                IsNonPublic = prop.DeclaredAccessibility != Accessibility.Public
            };
            into.Add(seg);
            current = prop.Type;
        }
    }

    private static bool ImplementsNotify(ITypeSymbol t) => t.AllInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged");
}

internal sealed class InvocationModel
{
    public string Kind { get; }
    public SimpleLambdaExpressionSyntax? FromLambda { get; }
    public SimpleLambdaExpressionSyntax? ToLambda { get; }
    public SimpleLambdaExpressionSyntax? WhenLambda { get; }
    public string? FromPath { get; }
    public string? ToPath { get; }
    public string? WhenPath { get; }
    public Location Location { get; }
    public List<PropertySegment> FromSegments { get; set; } = new();
    public List<PropertySegment> ToSegments { get; set; } = new();
    public string AssemblyName { get; }
    public string? RootFromTypeName { get; set; }
    public string? FromLeafTypeName { get; set; }
    public string? RootTargetTypeName { get; set; }
    public string? TargetLeafTypeName { get; set; }
    public string? TargetArgTypeName { get; set; }
    public string? WhenRootTypeName { get; set; }
    public string? WhenLeafTypeName { get; set; }
    public InvocationModel(string kind, SimpleLambdaExpressionSyntax? fromLambda, SimpleLambdaExpressionSyntax? toLambda, SimpleLambdaExpressionSyntax? whenLambda, string? fromPath, string? toPath, string? whenPath, Location location, string assemblyName)
    {
        Kind = kind;
        FromLambda = fromLambda;
        ToLambda = toLambda;
        WhenLambda = whenLambda;
        FromPath = fromPath;
        ToPath = toPath;
        WhenPath = whenPath;
        Location = location;
        AssemblyName = assemblyName;
    }
}

internal sealed class PropertySegment
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string DeclaringTypeName { get; set; } = string.Empty;
    public bool IsReferenceType { get; set; }
    public bool IsNotify { get; set; }
    public bool HasSetter { get; set; }
    public bool SetterIsNonPublic { get; set; }
    public bool IsNonPublic { get; set; }
}

internal sealed class CodeEmitter
{
    public string Emit(ImmutableArray<InvocationModel> invocations, string assemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using R3;");
        sb.AppendLine("namespace R3Ext;");
        var asm = assemblyName;
        if (invocations.IsDefaultOrEmpty || invocations.Length == 0)
        {
            if (asm == "R3Ext")
            {
                // Emit full method signatures with CallerArgumentExpression attributes even if there are no local invocation models.
                sb.AppendLine("public static partial class R3BindingExtensions");
                sb.AppendLine("{");
                EmitBindOneWay(ImmutableArray<InvocationModel>.Empty, sb);
                EmitBindTwoWay(ImmutableArray<InvocationModel>.Empty, sb);
                EmitWhenChanged(ImmutableArray<InvocationModel>.Empty, sb);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("internal static class __R3BindingRegistration { [ModuleInitializer] internal static void Init(){ /* no binding invocations detected in this assembly */ } }");
            }
            return sb.ToString();
        }
        // 'asm' already defined above for empty/non-empty cases
        // Populate type name metadata per invocation
        foreach (var inv in invocations)
        {
            if (inv.Kind == "WhenChanged")
            {
                if (inv.FromSegments.Count > 0)
                {
                    inv.WhenRootTypeName = inv.FromSegments[0].DeclaringTypeName;
                    inv.WhenLeafTypeName = inv.FromSegments.Last().TypeName;
                }
            }
            else
            {
                if (inv.FromSegments.Count > 0)
                {
                    inv.RootFromTypeName = inv.FromSegments[0].DeclaringTypeName;
                    inv.FromLeafTypeName = inv.FromSegments.Last().TypeName;
                }
                if (inv.ToSegments.Count > 0)
                {
                    inv.RootTargetTypeName = inv.ToSegments[0].DeclaringTypeName;
                    inv.TargetLeafTypeName = inv.ToSegments.Last().TypeName;
                }
            }
        }
        if (asm == "R3Ext")
        {
            sb.AppendLine("public static partial class R3BindingExtensions");
            sb.AppendLine("{");
            EmitUnsafeAccessors(invocations, sb);
            EmitBindOneWay(invocations.Where(i => i.Kind == "BindOneWay").ToImmutableArray(), sb);
            EmitBindTwoWay(invocations.Where(i => i.Kind == "BindTwoWay").ToImmutableArray(), sb);
            EmitWhenChanged(invocations.Where(i => i.Kind == "WhenChanged").ToImmutableArray(), sb);
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("internal static class __R3BindingRegistration");
            sb.AppendLine("{");
            var owCount = invocations.Count(i => i.Kind == "BindOneWay");
            var twCount = invocations.Count(i => i.Kind == "BindTwoWay");
            var wcCount = invocations.Count(i => i.Kind == "WhenChanged");
            sb.AppendLine($"    // Generator summary for {asm}: OW={owCount}, TW={twCount}, WC={wcCount}");
            // Debug: list invocations and metadata readiness
            foreach (var inv in invocations)
            {
                if (inv.Kind == "BindOneWay" || inv.Kind == "BindTwoWay")
                {
                    var ok = inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null;
                    sb.AppendLine($"    // {inv.Kind}: from={Escape(inv.FromPath ?? "-")} to={Escape(inv.ToPath ?? "-")} ok={ok} rf={inv.RootFromTypeName ?? "-"} fl={inv.FromLeafTypeName ?? "-"} rt={inv.RootTargetTypeName ?? "-"} tl={inv.TargetLeafTypeName ?? "-"} ta={inv.TargetArgTypeName ?? "-"}");
                }
            }
            sb.AppendLine("    [ModuleInitializer] internal static void Init(){");
            foreach (var inv in invocations)
            {
                if (inv.Kind == "BindOneWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    var id = Hash(inv.FromPath + "|" + inv.ToPath);
                    sb.AppendLine($"        BindingRegistry.RegisterOneWay<{inv.RootFromTypeName},{inv.FromLeafTypeName},{inv.RootTargetTypeName},{inv.TargetLeafTypeName}>(\"{Escape(inv.FromPath)}\", \"{Escape(inv.ToPath)}\", (f,t,conv) => __RegBindOneWay_{id}(f,t,conv));");
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    var id = Hash(inv.FromPath + "|" + inv.ToPath);
                    sb.AppendLine($"        BindingRegistry.RegisterTwoWay<{inv.RootFromTypeName},{inv.FromLeafTypeName},{inv.RootTargetTypeName},{inv.TargetLeafTypeName}>(\"{Escape(inv.FromPath)}\", \"{Escape(inv.ToPath)}\", (f,t,ht,th) => __RegBindTwoWay_{id}(f,t,ht,th));");
                }
                else if (inv.Kind == "WhenChanged" && inv.WhenPath is not null && inv.WhenRootTypeName is not null && inv.WhenLeafTypeName is not null)
                {
                    var id = Hash(inv.WhenRootTypeName + "|" + inv.WhenPath);
                    var simple = Simple(inv.WhenRootTypeName);
                    var composite = simple + "|" + Escape(inv.WhenPath);
                    sb.AppendLine($"        BindingRegistry.RegisterWhenChanged<{inv.WhenRootTypeName},{inv.WhenLeafTypeName}>(\"{composite}\", obj => __RegWhenChanged_{id}(obj));");
                }
            }
            sb.AppendLine("    }");
            var emitted = new System.Collections.Generic.HashSet<string>();
            foreach (var inv in invocations)
            {
                if (inv.Kind == "BindOneWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    var id = Hash(inv.FromPath + "|" + inv.ToPath);
                    if (emitted.Add("ow" + id)) EmitOneWayRegistrationBody(id, inv, sb);
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    var id = Hash(inv.FromPath + "|" + inv.ToPath);
                    if (emitted.Add("tw" + id)) EmitTwoWayRegistrationBody(id, inv, sb);
                }
                else if (inv.Kind == "WhenChanged" && inv.WhenPath is not null && inv.WhenRootTypeName is not null && inv.WhenLeafTypeName is not null)
                {
                    var id = Hash(inv.WhenRootTypeName + "|" + inv.WhenPath);
                    if (emitted.Add("wc" + id)) EmitWhenRegistrationBody(id, inv, sb);
                }
            }
            sb.AppendLine("}");
        }
        return sb.ToString();
    }

    private static string Hash(string s)
    {
        // FNV-1a 32-bit for compact method names
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in s)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }
    private static string Simple(string full)
    {
        if (full.StartsWith("global::")) full = full.Substring(8);
        var idx = full.LastIndexOf('.');
        return idx >= 0 ? full.Substring(idx + 1) : full;
    }

    private void EmitBindOneWay(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine("    public static partial IDisposable BindOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty>? conversionFunc = null, [CallerArgumentExpression(\"fromProperty\")] string? fromPropertyPath = null, [CallerArgumentExpression(\"toProperty\")] string? toPropertyPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromPropertyPath is not null && toPropertyPath is not null && BindingRegistry.TryCreateOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(fromPropertyPath, toPropertyPath, fromObject, targetObject, conversionFunc, out var __regDisp)) return __regDisp;");
        if (!models.IsDefaultOrEmpty)
        {
            sb.AppendLine("        switch (fromPropertyPath)");
            sb.AppendLine("        {");
            foreach (var m in models)
            {
                if (m.FromPath is null || m.ToPath is null) continue;
                var key1 = m.FromPath;
                var key2 = m.ToPath;
                var id = Hash(key1 + "|" + key2);
                sb.AppendLine($"            case \"{Escape(key1)}\":");
                sb.AppendLine("                switch (toPropertyPath)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    case \"{Escape(key2)}\": return __BindOneWay_{id}(fromObject, targetObject, conversionFunc);");
                sb.AppendLine("                }");
                sb.AppendLine("                break;");
            }
            sb.AppendLine("        }");
        }
        sb.AppendLine("        throw new NotSupportedException(\"No generated binding for provided property paths.\");");
        sb.AppendLine("    }");

        // Emit method bodies for each binding
        foreach (var m in models)
        {
            if (m.FromPath is null || m.ToPath is null || m.FromLambda is null || m.ToLambda is null) continue;
            var id = Hash(m.FromPath + "|" + m.ToPath);
            EmitOneWayBody(id, m, sb);
        }
    }

    private void EmitBindTwoWay(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine("    public static partial IDisposable BindTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty>? hostToTargetConv = null, Func<TTargetProperty,TFromProperty>? targetToHostConv = null, [CallerArgumentExpression(\"fromProperty\")] string? fromPropertyPath = null, [CallerArgumentExpression(\"toProperty\")] string? toPropertyPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromPropertyPath is not null && toPropertyPath is not null && BindingRegistry.TryCreateTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(fromPropertyPath, toPropertyPath, fromObject, targetObject, hostToTargetConv, targetToHostConv, out var __regDisp)) return __regDisp;");
        if (!models.IsDefaultOrEmpty)
        {
            sb.AppendLine("        switch (fromPropertyPath)");
            sb.AppendLine("        {");
            foreach (var m in models)
            {
                if (m.FromPath is null || m.ToPath is null) continue;
                var key1 = m.FromPath;
                var key2 = m.ToPath;
                var id = Hash(key1 + "|" + key2);
                sb.AppendLine($"            case \"{Escape(key1)}\":");
                sb.AppendLine("                switch (toPropertyPath)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    case \"{Escape(key2)}\": return __BindTwoWay_{id}(fromObject, targetObject, hostToTargetConv, targetToHostConv);");
                sb.AppendLine("                }");
                sb.AppendLine("                break;");
            }
            sb.AppendLine("        }");
        }
        sb.AppendLine("        throw new NotSupportedException(\"No generated two-way binding for provided property paths.\");");
        sb.AppendLine("    }");

        foreach (var m in models)
        {
            if (m.FromPath is null || m.ToPath is null || m.FromLambda is null || m.ToLambda is null) continue;
            var id = Hash(m.FromPath + "|" + m.ToPath);
            EmitTwoWayBody(id, m, sb);
        }
    }

    private void EmitWhenChanged(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine("    public static partial Observable<TReturn> WhenChanged<TObj,TReturn>(this TObj objectToMonitor, Expression<Func<TObj,TReturn>> propertyExpression, [CallerArgumentExpression(\"propertyExpression\")] string? propertyExpressionPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (propertyExpressionPath is not null){ var __key = typeof(TObj).Name + \"|\" + propertyExpressionPath; if (BindingRegistry.TryCreateWhenChanged<TObj,TReturn>(__key, objectToMonitor, out var __obs)) return __obs; }");
        if (!models.IsDefaultOrEmpty)
        {
            sb.AppendLine("        switch (propertyExpressionPath)");
            sb.AppendLine("        {");
            foreach (var m in models)
            {
                if (m.WhenPath is null) continue;
                var id = Hash(m.WhenRootTypeName + "|" + m.WhenPath);
                sb.AppendLine($"            case \"{Escape(m.WhenPath)}\": return __WhenChanged_{id}(objectToMonitor);");
            }
            sb.AppendLine("        }");
        }
        sb.AppendLine("        throw new NotSupportedException(\"No generated WhenChanged for provided property expression.\");");
        sb.AppendLine("    }");

        foreach (var m in models)
        {
            if (m.WhenPath is null || m.WhenLambda is null) continue;
            var id = Hash(m.WhenRootTypeName + "|" + m.WhenPath);
            EmitWhenBody(id, m, sb);
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private void EmitOneWayBody(string id, InvocationModel m, StringBuilder sb)
    {
        // n-level chain handling with INPC wiring and fallback EveryValueChanged for non-INPC segments
        sb.AppendLine($"    private static IDisposable __BindOneWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (TTargetProperty)(object?)v!;");
        // compute and wire host chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            var access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"        {seg.TypeName} __host_{i} = default!;");
        }
        sb.AppendLine("        // compute and wire target chain lazily inside Push");
        // Handler declarations (assigned after RewireHost/Push declared to avoid self-referential definite assignment issues)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }
        sb.AppendLine("        var __disposables = new System.Collections.Generic.List<IDisposable>();");
        // RewireHost implementation
        sb.AppendLine("        void RewireHost(){");
        sb.AppendLine("            // detach all handlers");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }
        // recompute chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }
        // reattach
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }
        sb.AppendLine("        }");
        // Push implementation
        sb.AppendLine("        void Push(){");
        // compute leaf value with try-catch
        var hostLeafAccess = BuildChainAccess("fromObject", m.FromSegments);
        var targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convert(v)");
        sb.AppendLine("            try { var v = (TFromProperty)(object?)" + hostLeafAccess + "!; " + targetLeafAssign + " } catch { } ");
        sb.AppendLine("        }");
        // Assign handlers now that helper methods exist
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine($"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{nextProp}\") {{ RewireHost(); Push(); }} if (e.PropertyName == \"{m.FromSegments.Last().Name}\") Push(); }};");
        }
        // initial wire
        sb.AppendLine("        RewireHost();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }
        // fallback EveryValueChanged
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(fromObject, _ => { try { return " + hostLeafAccess + "; } catch { return default!; } }).Subscribe(_ => Push())); } catch { }");
        sb.AppendLine("        Push();");
        sb.AppendLine("        return Disposable.Create(() => { foreach (var d in __disposables) d.Dispose();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }
        sb.AppendLine("        });");
        sb.AppendLine("    }");
    }

    private void EmitTwoWayBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static IDisposable __BindTwoWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? hostToTarget, Func<TTargetProperty,TFromProperty>? targetToHost)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTarget ??= v => (TTargetProperty)(object?)v!;");
        sb.AppendLine("        targetToHost ??= v => (TFromProperty)(object?)v!;");
        sb.AppendLine("        bool __updating = false;");
        sb.AppendLine("        var __disposables = new System.Collections.Generic.List<IDisposable>();");
        // host chain vars
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            sb.AppendLine($"        {seg.TypeName} __host_{i} = default!;");
        }
        // target chain vars
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            sb.AppendLine($"        {seg.TypeName} __target_{i} = default!;");
        }
        // Handlers for host
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireHost(); UpdateTarget(); }} if(e.PropertyName==\"{m.FromSegments.Last().Name}\") UpdateTarget(); }};");
        }
        // Handlers for target
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < m.ToSegments.Count ? m.ToSegments[i + 1].Name : m.ToSegments.Last().Name;
            sb.AppendLine($"        PropertyChangedEventHandler __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireTarget(); UpdateHost(); }} if(e.PropertyName==\"{m.ToSegments.Last().Name}\") UpdateHost(); }};");
        }
        // RewireHost
        sb.AppendLine("        void RewireHost(){");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }
        sb.AppendLine("        }");
        // RewireTarget
        sb.AppendLine("        void RewireTarget(){");
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT_{i}) npcT_{i}.PropertyChanged -= __h_target_{i};");
        }
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var access = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __target_{i} = {access}; }} catch {{ __target_{i} = default!; }}");
        }
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT2_{i}) npcT2_{i}.PropertyChanged += __h_target_{i};");
        }
        sb.AppendLine("        }");
        // UpdateTarget and UpdateHost
        var fromLeaf = BuildChainAccess("fromObject", m.FromSegments);
        var toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "hostToTarget(v)");
        var toLeafRead = BuildChainAccess("targetObject", m.ToSegments);
        var fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "targetToHost(v)");
        sb.AppendLine("        void UpdateTarget(){ if (__updating) return; __updating = true; try { var v = (TFromProperty)(object?)" + fromLeaf + "!; " + toLeafAssign + " } catch { } finally { __updating = false; } }");
        sb.AppendLine("        void UpdateHost(){ if (__updating) return; __updating = true; try { var v = (TTargetProperty)(object?)" + toLeafRead + "!; " + fromLeafAssign + " } catch { } finally { __updating = false; } }");
        // Initial wire
        sb.AppendLine("        RewireHost(); RewireTarget();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"        if(__target_{i} is INotifyPropertyChanged npc_initT_{i}) npc_initT_{i}.PropertyChanged += __h_target_{i};");
        }
        // fallback EveryValueChanged for both sides
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(fromObject, _ => { try { return " + fromLeaf + "; } catch { return default!; } }).Subscribe(_ => UpdateTarget())); } catch { }");
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(targetObject, _ => { try { return " + toLeafRead + "; } catch { return default!; } }).Subscribe(_ => UpdateHost())); } catch { }");
        sb.AppendLine("        UpdateTarget();");
        sb.AppendLine("        return Disposable.Create(() => { foreach (var d in __disposables) d.Dispose();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npc3T_{i}) npc3T_{i}.PropertyChanged -= __h_target_{i};");
        }
        sb.AppendLine("        });");
        sb.AppendLine("    }");
    }

    private void EmitWhenBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static Observable<TReturn> __WhenChanged_{id}<TObj,TReturn>(TObj root)");
        sb.AppendLine("    {");
        // If we don't have segment metadata (empty or any non-notify), fallback to EveryValueChanged
        var allNotify = m.FromSegments.Count > 0 && m.FromSegments.Take(m.FromSegments.Count - 1).All(s => s.IsNotify);
        if (!allNotify || m.FromSegments.Count == 0)
        {
            var getterFallback = ExtractMemberAccess(m.WhenLambda!, rootParam: "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, _ => { try { return " + getterFallback + "; } catch { return default!; } }).Select(v => (TReturn)v).DistinctUntilChanged(); } catch { return Observable.Empty<TReturn>(); }");
            sb.AppendLine("    }");
            return;
        }
        // Chain-aware observable using INPC handlers (reflection-free)
        sb.AppendLine("        if (root is null) return Observable.Empty<TReturn>();");
        // segment vars
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        }
        // handler declarations only for notify-capable segment objects (exclude leaf value segment if non-INPC)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }
        var leafAccess = BuildChainAccess("root", m.FromSegments);
        sb.AppendLine("        return Observable.Create<TReturn>(observer => {");
        sb.AppendLine("            void Emit(){ try { observer.OnNext((TReturn)(object?)" + leafAccess + "!); } catch { observer.OnNext(default!); } }");
        // Rewire method (re-evaluate chain and reattach handlers)
        sb.AppendLine("            void Rewire(){");
        // detach existing
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_det_{i}) npc_det_{i}.PropertyChanged -= __h_host_{i};");
        }
        // recompute chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"                try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }
        // reattach
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_att_{i}) npc_att_{i}.PropertyChanged += __h_host_{i};");
        }
        sb.AppendLine("            }");
        // assign handlers
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine($"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{nextProp}\") {{ Rewire(); Emit(); }} if(e.PropertyName == \"{m.FromSegments.Last().Name}\") Emit(); }};");
        }
        // initial wire
        sb.AppendLine("            Rewire(); Emit();");
        sb.AppendLine("            return Disposable.Create(() => {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_fin_{i}) npc_fin_{i}.PropertyChanged -= __h_host_{i};");
        }
        sb.AppendLine("            });");
        sb.AppendLine("        }).DistinctUntilChanged();");
        sb.AppendLine("    }");
    }

    // Registration bodies for non-core assemblies (concrete types)
    private void EmitOneWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static IDisposable __RegBindOneWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        for (int i = 0; i < m.FromSegments.Count; i++) sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        // Handler declarations for registration body (assignment postponed until after helpers)
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;"); }
        sb.AppendLine("        var __disposables = new System.Collections.Generic.List<IDisposable>();");
        sb.AppendLine("        void RewireHost(){");
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};"); }
        for (int i = 0; i < m.FromSegments.Count; i++) { var access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList()); sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}"); }
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};"); }
        sb.AppendLine("        }");
        sb.AppendLine("        void Push(){");
        var hostLeafAccess = BuildChainAccess("fromObject", m.FromSegments);
        var targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convert(v)");
        sb.AppendLine("            try { var v = (" + m.FromLeafTypeName + ")(object?)" + hostLeafAccess + "!; " + targetLeafAssign + " } catch { } ");
        sb.AppendLine("        }");
        // Assign handlers
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                // Leaf object: emit on leaf property changes only
                sb.AppendLine($"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{m.FromSegments.Last().Name}\") Push(); }};");
            }
            else
            {
                var nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{nextProp}\") {{ RewireHost(); Push(); }} }};");
            }
        }
        sb.AppendLine("        RewireHost();");
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};"); }
        // Use lambda parameter to ensure EveryValueChanged tracks the property chain
        var hostLeafAccessParam = BuildChainAccess("h", m.FromSegments);
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(fromObject, h => " + hostLeafAccessParam + ").Subscribe(_ => Push())); } catch { }");
        // Root handler for first segment replacement
        if (m.FromSegments.Count > 0)
        {
            var firstProp = m.FromSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_root = (s,e) => { if(e.PropertyName == \"" + firstProp + "\") { RewireHost(); Push(); } }; ");
            sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_root) npc_root.PropertyChanged += __h_root;");
        }
        sb.AppendLine("        Push();");
        sb.AppendLine("        return Disposable.Create(() => { foreach (var d in __disposables) d.Dispose();");
        if (m.FromSegments.Count > 0)
        {
            sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_root2) npc_root2.PropertyChanged -= __h_root;");
        }
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};"); }
        sb.AppendLine("        });");
        sb.AppendLine("    }");
    }
    private void EmitTwoWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static IDisposable __RegBindTwoWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? hostToTargetConv, Func<{m.TargetLeafTypeName},{m.FromLeafTypeName}>? targetToHostConv)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTargetConv ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        targetToHostConv ??= v => (" + m.FromLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        bool __updating = false; var __disposables = new System.Collections.Generic.List<IDisposable>();");
        for (int i = 0; i < m.FromSegments.Count; i++) sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        for (int i = 0; i < m.ToSegments.Count; i++) sb.AppendLine($"        {m.ToSegments[i].TypeName} __target_{i} = default!;");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            var seg = m.FromSegments[i];
            if (!seg.IsNotify) continue;
            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{m.FromSegments.Last().Name}\") UpdateTarget(); }};");
            }
            else
            {
                var nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireHost(); UpdateTarget(); }} }};");
            }
        }
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            var seg = m.ToSegments[i];
            if (!seg.IsNotify) continue;
            bool isLeafOwner = i == m.ToSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"        PropertyChangedEventHandler __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{m.ToSegments.Last().Name}\") UpdateHost(); }};");
            }
            else
            {
                var nextProp = m.ToSegments[i + 1].Name;
                sb.AppendLine($"        PropertyChangedEventHandler __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireTarget(); UpdateHost(); }} }};");
            }
        }
        sb.AppendLine("        void RewireHost(){");
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};"); }
        for (int i = 0; i < m.FromSegments.Count; i++) { var access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList()); sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}"); }
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};"); }
        sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
        sb.AppendLine("        }");
        sb.AppendLine("        void RewireTarget(){");
        for (int i = 0; i < m.ToSegments.Count; i++) { var seg = m.ToSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT_{i}) npcT_{i}.PropertyChanged -= __h_target_{i};"); }
        for (int i = 0; i < m.ToSegments.Count; i++) { var access = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList()); sb.AppendLine($"            try {{ __target_{i} = {access}; }} catch {{ __target_{i} = default!; }}"); }
        for (int i = 0; i < m.ToSegments.Count; i++) { var seg = m.ToSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT2_{i}) npcT2_{i}.PropertyChanged += __h_target_{i};"); }
        sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
        sb.AppendLine("        }");
        var fromLeaf = BuildChainAccess("fromObject", m.FromSegments);
        var toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "hostToTargetConv(v)");
        var toLeafRead = BuildChainAccess("targetObject", m.ToSegments);
        var fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "targetToHostConv(v)");
        sb.AppendLine("        void UpdateTarget(){ if (__updating) return; __updating = true; try { var v = (" + m.FromLeafTypeName + ")(object?)" + fromLeaf + "!; " + toLeafAssign + " R3ExtGeneratedInstrumentation.BindUpdates++; } catch { } finally { __updating = false; } }");
        sb.AppendLine("        void UpdateHost(){ if (__updating) return; __updating = true; try { var v = (" + m.TargetLeafTypeName + ")(object?)" + toLeafRead + "!; " + fromLeafAssign + " R3ExtGeneratedInstrumentation.BindUpdates++; } catch { } finally { __updating = false; } }");
        sb.AppendLine("        RewireHost(); RewireTarget();");
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};"); }
        for (int i = 0; i < m.ToSegments.Count; i++) { var seg = m.ToSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"        if(__target_{i} is INotifyPropertyChanged npc_initT_{i}) npc_initT_{i}.PropertyChanged += __h_target_{i};"); }
        if (m.FromSegments.Count > 0) { var firstHostProp = m.FromSegments[0].Name; sb.AppendLine("        PropertyChangedEventHandler __h_rootHost = (s,e)=>{ if(e.PropertyName==\"" + firstHostProp + "\") UpdateTarget(); }; "); sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_rootHost) npc_rootHost.PropertyChanged += __h_rootHost;"); }
        if (m.ToSegments.Count > 0) { var firstTargetProp = m.ToSegments[0].Name; sb.AppendLine("        PropertyChangedEventHandler __h_rootTarget = (s,e)=>{ if(e.PropertyName==\"" + firstTargetProp + "\") UpdateHost(); }; "); sb.AppendLine("        if(targetObject is INotifyPropertyChanged npc_rootTarget) npc_rootTarget.PropertyChanged += __h_rootTarget;"); }
        var fromLeafParam = BuildChainAccess("h", m.FromSegments);
        var toLeafParam = BuildChainAccess("t", m.ToSegments);
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(fromObject, h => " + fromLeafParam + ").Subscribe(_ => UpdateTarget())); } catch { }");
        sb.AppendLine("        try { __disposables.Add(Observable.EveryValueChanged(targetObject, t => " + toLeafParam + ").Subscribe(_ => UpdateHost())); } catch { }");
        sb.AppendLine("        UpdateTarget();");
        sb.AppendLine("        return Disposable.Create(() => { foreach (var d in __disposables) d.Dispose();");
        for (int i = 0; i < m.FromSegments.Count; i++) { var seg = m.FromSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};"); }
        for (int i = 0; i < m.ToSegments.Count; i++) { var seg = m.ToSegments[i]; if (!seg.IsNotify) continue; sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npc3T_{i}) npc3T_{i}.PropertyChanged -= __h_target_{i};"); }
        if (m.FromSegments.Count > 0) sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_rootHost2) npc_rootHost2.PropertyChanged -= __h_rootHost;");
        if (m.ToSegments.Count > 0) sb.AppendLine("            if(targetObject is INotifyPropertyChanged npc_rootTarget2) npc_rootTarget2.PropertyChanged -= __h_rootTarget;");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
    }
    private void EmitWhenRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static Observable<{m.WhenLeafTypeName}> __RegWhenChanged_{id}({m.WhenRootTypeName} root)");
        sb.AppendLine("    {");
        var allNotify = m.FromSegments.Count > 0 && m.FromSegments.Take(m.FromSegments.Count - 1).All(s => s.IsNotify);
        if (!allNotify || m.FromSegments.Count == 0)
        {
            var getterFallback = ExtractMemberAccess(m.WhenLambda!, rootParam: "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, _ => { try { return " + getterFallback + "; } catch { return default!; } }).Select(v => (" + m.WhenLeafTypeName + ")v).DistinctUntilChanged(); } catch { return Observable.Empty<" + m.WhenLeafTypeName + ">(); }");
            sb.AppendLine("    }");
            return;
        }
        sb.AppendLine("        if (root is null) return Observable.Empty<" + m.WhenLeafTypeName + ">();");
        for (int i = 0; i < m.FromSegments.Count; i++) sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        for (int i = 0; i < m.FromSegments.Count; i++) if (m.FromSegments[i].IsNotify) sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        var leafAccess = BuildChainAccess("root", m.FromSegments);
        sb.AppendLine("        return Observable.Create<" + m.WhenLeafTypeName + ">(observer => {");
        sb.AppendLine("            void Emit(){ try { observer.OnNext((" + m.WhenLeafTypeName + ")(object?)" + leafAccess + "!); } catch { observer.OnNext(default!); } }");
        sb.AppendLine("            void Rewire(){");
        for (int i = 0; i < m.FromSegments.Count; i++) if (m.FromSegments[i].IsNotify) sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_det_{i}) npc_det_{i}.PropertyChanged -= __h_host_{i};");
        for (int i = 0; i < m.FromSegments.Count; i++) { var access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList()); sb.AppendLine($"                try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}"); }
        for (int i = 0; i < m.FromSegments.Count; i++) if (m.FromSegments[i].IsNotify) sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_att_{i}) npc_att_{i}.PropertyChanged += __h_host_{i};");
        sb.AppendLine("            }");
        // Root handler to detect replacement of first segment object
        var firstProp = m.FromSegments.Count > 0 ? m.FromSegments[0].Name : null;
        sb.AppendLine("            PropertyChangedEventHandler __h_root = (s,e) => { if(e.PropertyName == \"" + firstProp + "\") { Rewire(); Emit(); } }; ");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify) continue;
            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{m.FromSegments.Last().Name}\") Emit(); }};");
            }
            else
            {
                var nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{nextProp}\") {{ Rewire(); Emit(); }} }};");
            }
        }
        sb.AppendLine("            Rewire(); Emit();");
        sb.AppendLine("            if(root is INotifyPropertyChanged npc_root) npc_root.PropertyChanged += __h_root;");
        sb.AppendLine("            return Disposable.Create(() => {");
        for (int i = 0; i < m.FromSegments.Count; i++) if (m.FromSegments[i].IsNotify) sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_fin_{i}) npc_fin_{i}.PropertyChanged -= __h_host_{i};");
        sb.AppendLine("                if(root is INotifyPropertyChanged npc_root2) npc_root2.PropertyChanged -= __h_root;");
        sb.AppendLine("            });");
        sb.AppendLine("        }).DistinctUntilChanged();");
        sb.AppendLine("    }");
    }

    // old placeholder helpers removed; replaced by chain-aware emission above

    private static string ExtractMemberAccess(SimpleLambdaExpressionSyntax lambda, string rootParam = null!)
    {
        // very limited: param => param.Member
        var paramName = rootParam ?? lambda.Parameter.Identifier.ValueText;
        if (lambda.Body is MemberAccessExpressionSyntax maes)
        {
            // Reconstruct chained access
            var parts = new Stack<string>();
            ExpressionSyntax? cur = maes;
            while (cur is MemberAccessExpressionSyntax mae)
            {
                parts.Push(mae.Name.Identifier.ValueText);
                cur = mae.Expression;
            }
            var sb = new StringBuilder();
            sb.Append(paramName);
            foreach (var p in parts) sb.Append('.').Append(p);
            return sb.ToString();
        }
        return "default!";
    }

    private static string ExtractTopMemberName(SimpleLambdaExpressionSyntax lambda)
    {
        if (lambda.Body is MemberAccessExpressionSyntax maes)
        {
            return maes.Name.Identifier.ValueText;
        }
        return string.Empty;
    }

    private static void EmitUnsafeAccessors(ImmutableArray<InvocationModel> invocations, StringBuilder sb)
    {
        var setList = new HashSet<string>();
        foreach (var inv in invocations)
        {
            if (inv.ToSegments.Count > 0)
            {
                var leaf = inv.ToSegments.Last();
                if (leaf.SetterIsNonPublic)
                {
                    var key = Sanitize(leaf.DeclaringTypeName) + "_" + Sanitize(leaf.Name);
                    if (setList.Add(key))
                    {
                        sb.AppendLine("#if NET8_0_OR_GREATER || NET9_0_OR_GREATER");
                        sb.AppendLine($"    [UnsafeAccessor(UnsafeAccessorKind.Setter, Name=\"{leaf.Name}\")] private static extern void __UA_SET_{key}({leaf.DeclaringTypeName} instance, {leaf.TypeName} value);");
                        sb.AppendLine("#endif");
                    }
                }
            }
        }
        static string Sanitize(string s)
        {
            var sb2 = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb2.Append(ch); else sb2.Append('_');
            }
            return sb2.ToString();
        }
    }

    private static string BuildChainAccess(string root, List<PropertySegment> segments)
    {
        var sb = new StringBuilder(root);
        foreach (var seg in segments)
            sb.Append('.').Append(seg.Name);
        return sb.ToString();
    }

    private static string BuildLeafAssignmentSet(string root, List<PropertySegment> segments, string valueExpr)
    {
        if (segments.Count == 0) return "";
        if (segments.Count == 1)
        {
            var leaf = segments[0];
            var key = SanKey(leaf);
            var direct = root + "." + leaf.Name + " = " + valueExpr + ";";
            if (leaf.SetterIsNonPublic)
            {
                return $"#if NET8_0_OR_GREATER || NET9_0_OR_GREATER\n                __UA_SET_{key}({root}, ({leaf.TypeName}) (object?){valueExpr}!);\n#else\n                {direct}\n#endif";
            }
            return direct;
        }
        else
        {
            var containerAccess = BuildChainAccess(root, segments.Take(segments.Count - 1).ToList());
            var leaf = segments.Last();
            var key = SanKey(leaf);
            var direct = containerAccess + "." + leaf.Name + " = " + valueExpr + ";";
            return $"if(({containerAccess}) != null) {{ " + (leaf.SetterIsNonPublic ? $"#if NET8_0_OR_GREATER || NET9_0_OR_GREATER\n                __UA_SET_{key}({containerAccess}, ({leaf.TypeName}) (object?){valueExpr}!);\n#else\n                {direct}\n#endif" : direct) + " }";
        }

        static string SanKey(PropertySegment leaf)
        {
            var t = new StringBuilder();
            foreach (var ch in leaf.DeclaringTypeName) t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            t.Append('_');
            foreach (var ch in leaf.Name) t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return t.ToString();
        }
    }
}
