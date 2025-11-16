using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace R3Ext.PropertyChanged.SourceGenerator;

// Unified incremental generator emitting robust nested-chain implementations for WhenChanged/WhenChanging, BindOneWay, BindTwoWay.
// Also emits optional specialized fast variants (WhenChangedFast / BindTwoWayFast) for simple member chains.
[Generator]
public sealed class PropertyChangedIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (ctx, _) => TransformInvocation(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .Collect();
        var strictMode = context.AnalyzerConfigOptionsProvider
            .Select(static (p, _) => {
                p.GlobalOptions.TryGetValue("build_property.R3ExtStrict", out var value);
                return value is not null && value.Equals("true", StringComparison.OrdinalIgnoreCase);
            });
        var combined = invocations.Combine(strictMode);
        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var rawInvocations = tuple.Left;
            var isStrict = tuple.Right;
            if (rawInvocations.Length == 0) return;
            // Emit diagnostics for non-specializable chains in strict mode.
            if (isStrict)
            {
                foreach (var inv in rawInvocations.Where(r => r.MethodKind is "BindOneWay" or "BindTwoWay"))
                {
                    if (!inv.CanSpecialize && inv.Location is not null)
                    {
                        var desc = new DiagnosticDescriptor(
                            id: "R3EXT001",
                            title: "Strict mode: chain not specialized",
                            messageFormat: "Strict mode requires specialization; chain for method '{0}' cannot be specialized (non-public or unsupported segment).",
                            category: "R3Ext.Binding",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true);
                        spc.ReportDiagnostic(Diagnostic.Create(desc, inv.Location, inv.MethodKind));
                    }
                }
            }
            // Advisory diagnostics outside strict mode for unsupported segments
            foreach (var inv in rawInvocations.Where(r => r.MethodKind is "BindOneWay" or "BindTwoWay"))
            {
                if (inv.UnsupportedReason is not null && inv.Location is not null)
                {
                    var desc2 = new DiagnosticDescriptor(
                        id: "R3EXT002",
                        title: "Chain contains unsupported segment",
                        messageFormat: "Binding chain not specialized: {0}",
                        category: "R3Ext.Binding",
                        DiagnosticSeverity.Info,
                        isEnabledByDefault: true);
                    spc.ReportDiagnostic(Diagnostic.Create(desc2, inv.Location, inv.UnsupportedReason));
                }
            }
            var source = GenerateUnified(rawInvocations, isStrict);
            spc.AddSource("R3Ext.PropertyChanged.BindingAndNotify.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static InvocationModel? TransformInvocation(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not InvocationExpressionSyntax invocation) return null;
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax maes => maes.Name.Identifier.Text,
            MemberBindingExpressionSyntax mbes => mbes.Name.Identifier.Text,
            IdentifierNameSyntax ins => ins.Identifier.Text,
            _ => null
        };
        if (methodName is null) return null;
        if (methodName is not ("WhenChanged" or "WhenChanging" or "BindOneWay" or "BindTwoWay" or "WhenChangedFast" or "BindTwoWayFast")) return null;

        var model = new InvocationModel { MethodKind = methodName, Location = invocation.GetLocation() };

        // Attempt specialization data extraction for simple leaf properties (BindTwoWay & BindOneWay)
        if (methodName == "BindTwoWay" || methodName == "BindOneWay")
        {
            try
            {
                // Extension receiver (fromObject)
                if (invocation.Expression is MemberAccessExpressionSyntax maes)
                {
                    var fromType = ctx.SemanticModel.GetTypeInfo(maes.Expression).Type;
                    model.FromRootType = fromType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
                }
                // Arguments: targetObject, fromProperty lambda, toProperty lambda, optional converters...
                if (invocation.ArgumentList.Arguments.Count >= 3)
                {
                    var targetExpr = invocation.ArgumentList.Arguments[0].Expression;
                    var targetType = ctx.SemanticModel.GetTypeInfo(targetExpr).Type;
                    model.TargetRootType = targetType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;

                    if (invocation.ArgumentList.Arguments[1].Expression is SimpleLambdaExpressionSyntax fromLambda &&
                        invocation.ArgumentList.Arguments[2].Expression is SimpleLambdaExpressionSyntax toLambda)
                    {
                        model.FromLambdaText = fromLambda.ToString();
                        model.ToLambdaText = toLambda.ToString();
                        var fromLeafName = model.FromLeafPropertyName; var fromLeafType = model.FromLeafPropertyType;
                        ExtractChain(ctx, fromLambda, model.FromChainSegments, ref fromLeafName, ref fromLeafType, model);
                        model.FromLeafPropertyName = fromLeafName; model.FromLeafPropertyType = fromLeafType;
                        var toLeafName = model.TargetLeafPropertyName; var toLeafType = model.TargetLeafPropertyType;
                        ExtractChain(ctx, toLambda, model.TargetChainSegments, ref toLeafName, ref toLeafType, model);
                        model.TargetLeafPropertyName = toLeafName; model.TargetLeafPropertyType = toLeafType;
                                    model.FromChainKey = string.Join(".", model.FromChainSegments.Select(s=>s.Name));
                                    model.TargetChainKey = string.Join(".", model.TargetChainSegments.Select(s=>s.Name));
                        model.CanSpecialize = model.FromChainSegments.Count > 0 && model.TargetChainSegments.Count > 0 && model.UnsupportedReason is null;
                    }
                }
            }
            catch
            {
                model.CanSpecialize = false; // robust fallback
            }
        }
        return model;
    }

    private static void ExtractChain(GeneratorSyntaxContext ctx, SimpleLambdaExpressionSyntax lambda, List<PropertySegment> segments, ref string leafName, ref string leafType, InvocationModel owning)
    {
        segments.Clear();
        if (lambda.Body is not MemberAccessExpressionSyntax) return;
        // Traverse chain from leaf upward.
        var stack = new Stack<MemberAccessExpressionSyntax>();
        ExpressionSyntax? cur = lambda.Body as ExpressionSyntax;
        while (cur is MemberAccessExpressionSyntax mae)
        {
            stack.Push(mae);
            cur = mae.Expression;
        }
        if (cur is not IdentifierNameSyntax) return; // parameter root required
        // Pop to get root->leaf order
        while (stack.Count > 0)
        {
            var mae = stack.Pop();
            var sym = ctx.SemanticModel.GetSymbolInfo(mae).Symbol as IPropertySymbol;
            if (sym == null)
            {
                owning.UnsupportedReason = "Unsupported segment (not a property)";
                segments.Clear();
                return;
            }
            var seg = new PropertySegment
            {
                Name = sym.Name,
                TypeName = sym.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsNotify = ImplementsNotify(sym.Type),
                IsNonPublic = sym.DeclaredAccessibility != Accessibility.Public,
                HasSetter = sym.SetMethod is not null,
                DeclaringTypeName = sym.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            };
            segments.Add(seg);
            leafName = seg.Name;
            leafType = seg.TypeName;
        }
    }

    private static bool ImplementsNotify(ITypeSymbol type) => type.AllInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged");

    private static string NormalizeLambdaString(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var arr = s.Where(c => !char.IsWhiteSpace(c)).ToArray();
        return new string(arr);
    }

    private static string GenerateUnified(IReadOnlyList<InvocationModel> invocations, bool strictMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;\nusing System.ComponentModel;\nusing System.Linq.Expressions;\nusing System.Collections.Generic;\nusing System.Collections.Concurrent;\nusing R3;\nusing System.Runtime.CompilerServices;");
        sb.AppendLine("namespace R3Ext.PropertyChanged { ");
        var kinds = new HashSet<string>(invocations.Select(x => x.MethodKind));
        if (kinds.Contains("WhenChanged") || kinds.Contains("WhenChanging") || kinds.Contains("WhenChangedFast"))
        {
            sb.AppendLine("internal static partial class NotifyPropertyExtensions {");
            if (kinds.Contains("WhenChanged")) sb.AppendLine(GenerateWhen("WhenChanged","INotifyPropertyChanged"));
            if (kinds.Contains("WhenChanging")) sb.AppendLine(GenerateWhen("WhenChanging","INotifyPropertyChanging"));
            if (kinds.Contains("WhenChangedFast")) sb.AppendLine(GenerateWhenFast());
            sb.AppendLine("}");
        }
        if (kinds.Contains("BindOneWay") || kinds.Contains("BindTwoWay") || kinds.Contains("BindTwoWayFast"))
        {
            sb.AppendLine("internal static partial class BindingExtensions {");
            // Global shared reflection caches keyed by chain key (dot-separated names)
            sb.AppendLine("    private static readonly ConcurrentDictionary<string, Dictionary<Type,System.Reflection.PropertyInfo?>[]> __PropertyChainCaches = new();");
            sb.AppendLine("    private static Dictionary<Type,System.Reflection.PropertyInfo?>[] __GetOrAddChainCaches(List<string> chain){ var key = string.Join('.', chain); return __PropertyChainCaches.GetOrAdd(key, _ => { var arr = new Dictionary<Type,System.Reflection.PropertyInfo?>[chain.Count]; for(int i=0;i<chain.Count;i++) arr[i]=new(); return arr; }); }");
            // UnsafeAccessor stubs for non-public properties (getter/setter)
            var nonPublicProps = invocations
                .Where(i => i.MethodKind is "BindOneWay" or "BindTwoWay")
                .SelectMany(i => i.FromChainSegments.Concat(i.TargetChainSegments))
                .Where(p => p.IsNonPublic)
                .Select(p => (p.DeclaringTypeName, p.Name, p.TypeName, p.HasSetter))
                .Distinct()
                .ToList();
            if (nonPublicProps.Count > 0)
            {
                sb.AppendLine("#if NET8_0_OR_GREATER || NET9_0_OR_GREATER");
                foreach (var (declType, propName, propType, hasSetter) in nonPublicProps)
                {
                    var safeDecl = Sanitize(declType);
                    var safeProp = Sanitize(propName);
                    sb.AppendLine($"[UnsafeAccessor(UnsafeAccessorKind.Getter, Name = \"{propName}\")] private static extern {propType} __UA_GET_{safeDecl}_{safeProp}({declType} instance);");
                    if (hasSetter)
                        sb.AppendLine($"[UnsafeAccessor(UnsafeAccessorKind.Setter, Name = \"{propName}\")] private static extern void __UA_SET_{safeDecl}_{safeProp}({declType} instance, {propType} value);");
                }
                sb.AppendLine("#endif");
            }
            // Emit specialized BindTwoWay overloads first (so they win overload resolution) for simple leaf property cases.
            var specializedTwoWay = invocations.Where(x => x.MethodKind == "BindTwoWay" && x.CanSpecialize)
                .GroupBy(x => x.SpecializationKey)
                .Select(g => g.First());
            foreach (var spec in specializedTwoWay)
                sb.AppendLine(GenerateBindTwoWaySpecialized(spec));

            var specializedOneWay = invocations.Where(x => x.MethodKind == "BindOneWay" && x.CanSpecialize)
                .GroupBy(x => x.SpecializationKey)
                .Select(g => g.First());
            foreach (var spec in specializedOneWay)
                sb.AppendLine(GenerateBindOneWaySpecialized(spec));
            // Emit public generic methods with dispatch to specialized helpers; include reflective fallback.
            if (kinds.Contains("BindOneWay")) sb.AppendLine(GenerateBindOneWayWithDispatch(specializedOneWay.ToList(), strictMode));
            if (kinds.Contains("BindTwoWay")) sb.AppendLine(GenerateBindTwoWayWithDispatch(specializedTwoWay.ToList(), strictMode));
            if (kinds.Contains("BindTwoWayFast")) sb.AppendLine(GenerateBindTwoWayFast());
            sb.AppendLine("}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateWhenFast()
    {
        return "public static Observable<TReturn> WhenChangedFast<TObj,TReturn>(this TObj objectToMonitor, Expression<Func<TObj,TReturn>> propertyExpression) where TObj:INotifyPropertyChanged {\n    if(objectToMonitor==null) return Observable.Empty<TReturn>();\n    if(propertyExpression.Body is not MemberExpression) return objectToMonitor.WhenChanged(propertyExpression);\n    List<MemberExpression> members = new(); Expression cur = propertyExpression.Body; while(cur is MemberExpression me){ members.Insert(0, me); cur = me.Expression; } if(members.Count==0 || members.Count>4) return objectToMonitor.WhenChanged(propertyExpression);\n    var getter = propertyExpression.Compile();\n    return Observable.Create<TReturn>(observer=>{\n        void Emit(){ try{ observer.OnNext(getter(objectToMonitor)); } catch { observer.OnNext(default!); } }\n        PropertyChangedEventHandler rootHandler = (s,e)=>{ if(e.PropertyName==members[0].Member.Name || e.PropertyName==members[members.Count-1].Member.Name) Emit(); };\n        objectToMonitor.PropertyChanged += rootHandler; Emit();\n        return Disposable.Create(()=> objectToMonitor.PropertyChanged -= rootHandler);\n    });\n}";
    }

    private static string GenerateBindTwoWayFast()
    {
        return "public static IDisposable BindTwoWayFast<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty> hostToTargetConv, Func<TTargetProperty,TFromProperty> targetToHostConv) where TFrom:class,INotifyPropertyChanged where TTarget:class,INotifyPropertyChanged {\n    if(fromObject==null||targetObject==null) return Disposable.Empty;\n    if(fromProperty.Body is not MemberExpression || toProperty.Body is not MemberExpression) return fromObject.BindTwoWay(targetObject, fromProperty, toProperty, hostToTargetConv, targetToHostConv);\n    var hostMember = ((MemberExpression)fromProperty.Body).Member.Name; var targetMember = ((MemberExpression)toProperty.Body).Member.Name;\n    var hostGetter = fromProperty.Compile(); var targetGetter = toProperty.Compile(); bool updating=false;\n    void PushToTarget(){ if(updating) return; try{ updating=true; var v = hostToTargetConv(hostGetter(fromObject)); var pi = targetObject.GetType().GetProperty(targetMember); if(pi!=null && pi.CanWrite) pi.SetValue(targetObject,v); } finally { updating=false; } }\n    void PushToHost(){ if(updating) return; try{ updating=true; var v = targetToHostConv(targetGetter(targetObject)); var pi = fromObject.GetType().GetProperty(hostMember); if(pi!=null && pi.CanWrite) pi.SetValue(fromObject,v); } finally { updating=false; } }\n    PropertyChangedEventHandler hHost = (s,e)=>{ if(e.PropertyName==hostMember) PushToTarget(); }; PropertyChangedEventHandler hTarget = (s,e)=>{ if(e.PropertyName==targetMember) PushToHost(); }; fromObject.PropertyChanged += hHost; targetObject.PropertyChanged += hTarget; PushToTarget(); return Disposable.Create(()=>{ fromObject.PropertyChanged -= hHost; targetObject.PropertyChanged -= hTarget; });\n}";
    }

    private static string GenerateBindTwoWaySpecialized(InvocationModel spec)
    {
        var methodSafe = "__BindTwoWay_" + Sanitize(spec.FromRootType) + "_" + Sanitize(spec.TargetRootType) + "_" + spec.SpecializationHash;
        var fromType = spec.FromRootType;
        var targetType = spec.TargetRootType;
        var hostLeafType = spec.FromLeafPropertyType;
        var targetLeafType = spec.TargetLeafPropertyType;
        string hostLeafAccess = BuildChainAccessUnsafe("fromObject", spec.FromChainSegments);
        string targetLeafAccess = BuildChainAccessUnsafe("targetObject", spec.TargetChainSegments);
        var code = new StringBuilder();
        code.AppendLine($"private static IDisposable {methodSafe}({fromType} fromObject, {targetType} targetObject, Func<{hostLeafType},{targetLeafType}> hostToTargetConv, Func<{targetLeafType},{hostLeafType}> targetToHostConv) {{");
        code.AppendLine("    if(fromObject==null||targetObject==null) return Disposable.Empty;");
        if(!spec.FromChainSegments.Any(s=>s.IsNotify) && !spec.TargetChainSegments.Any(s=>s.IsNotify))
        {
            code.AppendLine("    // Passive two-way chain: no intermediate notify segments, root-level handlers only");
            code.AppendLine("    bool _updating=false;");
            code.AppendLine("    void PushToTargetPassive(){ if(_updating) return; try{ _updating=true; var converted = hostToTargetConv(" + hostLeafAccess + "); " + targetLeafAccess + " = converted; } catch {} finally { _updating=false; } }");
            code.AppendLine("    void PushToHostPassive(){ if(_updating) return; try{ _updating=true; var converted = targetToHostConv(" + targetLeafAccess + "); " + hostLeafAccess + " = converted; } catch {} finally { _updating=false; } }");
            code.AppendLine($"    PropertyChangedEventHandler __rootHost = (s,e)=>{{ if(e.PropertyName==\"{spec.FromLeafPropertyName}\") PushToTargetPassive(); }};");
            code.AppendLine($"    PropertyChangedEventHandler __rootTarget = (s,e)=>{{ if(e.PropertyName==\"{spec.TargetLeafPropertyName}\") PushToHostPassive(); }};");
            code.AppendLine("    fromObject.PropertyChanged += __rootHost; targetObject.PropertyChanged += __rootTarget;");
            code.AppendLine("    PushToTargetPassive();");
            code.AppendLine("    return Disposable.Create(()=>{ fromObject.PropertyChanged -= __rootHost; targetObject.PropertyChanged -= __rootTarget; });");
            code.AppendLine("}");
            code.AppendLine("// No public overloads here; dispatch occurs in the generic expression-based method.");
            return code.ToString();
        }
        // Declare segment variables for host chain
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            var access = BuildChainAccess("fromObject", spec.FromChainSegments.Take(i+1).ToList());
            code.AppendLine($"    {seg.TypeName} __host_{i} = {access};");
        }
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            var access = BuildChainAccess("targetObject", spec.TargetChainSegments.Take(i+1).ToList());
            code.AppendLine($"    {seg.TypeName} __target_{i} = {access};");
        }
        code.AppendLine("    bool _updating=false;");
        code.AppendLine("    // Leaf property write caches");
        code.AppendLine("    static System.Reflection.PropertyInfo? __GetLeafPI(Type t,string name)=> t.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);");
        code.AppendLine($"    var __leafTargetWrite = __GetLeafPI(typeof({spec.TargetChainSegments.Last().DeclaringTypeName}), \"{spec.TargetChainSegments.Last().Name}\");");
        code.AppendLine($"    var __leafHostWrite = __GetLeafPI(typeof({spec.FromChainSegments.Last().DeclaringTypeName}), \"{spec.FromChainSegments.Last().Name}\");");
        code.AppendLine("    void PushToTarget(){ if(_updating) return; try{ _updating=true; var converted = hostToTargetConv(" + hostLeafAccess + "); " + BuildLeafAssignment("targetObject", spec.TargetChainSegments, "converted") + " } catch {} finally { _updating=false; } }");
        code.AppendLine("    void PushToHost(){ if(_updating) return; try{ _updating=true; var converted = targetToHostConv(" + targetLeafAccess + "); " + BuildLeafAssignment("fromObject", spec.FromChainSegments, "converted") + " } catch {} finally { _updating=false; } }");
        // Handlers for host chain segments
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < spec.FromChainSegments.Count ? spec.FromChainSegments[i + 1].Name : spec.FromLeafPropertyName;
            code.AppendLine($"    PropertyChangedEventHandler __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\") {{ RewireHost(); PushToTarget(); }} if(e.PropertyName==\"{spec.FromLeafPropertyName}\") PushToTarget(); }}; ");
        }
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < spec.TargetChainSegments.Count ? spec.TargetChainSegments[i + 1].Name : spec.TargetLeafPropertyName;
            code.AppendLine($"    PropertyChangedEventHandler __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\") {{ RewireTarget(); PushToHost(); }} if(e.PropertyName==\"{spec.TargetLeafPropertyName}\") PushToHost(); }}; ");
        }
        // RewireHost method
        code.AppendLine("    void RewireHost(){");
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            var access = BuildChainAccess("fromObject", spec.FromChainSegments.Take(i+1).ToList());
            code.AppendLine($"        var new_{i} = {access};");
            code.AppendLine($"        if(!ReferenceEquals(__host_{i}, new_{i})) {{ if(__host_{i} != null && {seg.IsNotify.ToString().ToLower()} ) {{ __host_{i}.PropertyChanged -= __h_host_{i}; }} __host_{i} = new_{i}; if(__host_{i} != null && {seg.IsNotify.ToString().ToLower()}) __host_{i}.PropertyChanged += __h_host_{i}; }}");
        }
        code.AppendLine("    }");
        // RewireTarget method
        code.AppendLine("    void RewireTarget(){");
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            var access = BuildChainAccess("targetObject", spec.TargetChainSegments.Take(i+1).ToList());
            code.AppendLine($"        var new_{i} = {access};");
            code.AppendLine($"        if(!ReferenceEquals(__target_{i}, new_{i})) {{ if(__target_{i} != null && {seg.IsNotify.ToString().ToLower()}) {{ __target_{i}.PropertyChanged -= __h_target_{i}; }} __target_{i} = new_{i}; if(__target_{i} != null && {seg.IsNotify.ToString().ToLower()}) __target_{i}.PropertyChanged += __h_target_{i}; }}");
        }
        code.AppendLine("    }");
        // Initial wiring
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"    if(__host_{i}!=null) __host_{i}.PropertyChanged += __h_host_{i};");
        }
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"    if(__target_{i}!=null) __target_{i}.PropertyChanged += __h_target_{i};");
        }
        code.AppendLine("    PushToTarget();");
        code.AppendLine("    return Disposable.Create(()=>{");
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"        if(__host_{i}!=null) __host_{i}.PropertyChanged -= __h_host_{i};");
        }
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"        if(__target_{i}!=null) __target_{i}.PropertyChanged -= __h_target_{i};");
        }
        code.AppendLine("    });");
        code.AppendLine("}");
        // No public overloads here; dispatch occurs in the generic expression-based method.
        return code.ToString();
    }

    private static string GenerateBindOneWaySpecialized(InvocationModel spec)
    {
        var methodSafe = "__BindOneWay_" + Sanitize(spec.FromRootType) + "_" + Sanitize(spec.TargetRootType) + "_" + spec.SpecializationHash;
        var fromType = spec.FromRootType;
        var targetType = spec.TargetRootType;
        var hostLeafType = spec.FromLeafPropertyType;
        var targetLeafType = spec.TargetLeafPropertyType;
        string hostLeafAccess = BuildChainAccessUnsafe("fromObject", spec.FromChainSegments);
        string targetLeafAccess = BuildChainAccessUnsafe("targetObject", spec.TargetChainSegments);
        var code = new StringBuilder();
        code.AppendLine($"private static IDisposable {methodSafe}({fromType} fromObject, {targetType} targetObject, Func<{hostLeafType},{targetLeafType}> hostToTargetConv) {{");
        code.AppendLine("    if(fromObject==null||targetObject==null) return Disposable.Empty;");
        if(!spec.FromChainSegments.Any(s=>s.IsNotify))
        {
            code.AppendLine("    // Passive chain: no intermediate notify segments, subscribe to root only");
            code.AppendLine("    void PushPassive(){ try{ var converted = hostToTargetConv(" + hostLeafAccess + "); " + BuildLeafAssignment("targetObject", spec.TargetChainSegments, "converted") + " } catch { } }");
            code.AppendLine($"    PropertyChangedEventHandler __root = (s,e)=>{{ if(e.PropertyName==\"{spec.FromLeafPropertyName}\") PushPassive(); }}; ");
            code.AppendLine("    fromObject.PropertyChanged += __root;");
            code.AppendLine("    PushPassive();");
            code.AppendLine("    return Disposable.Create(()=>{ fromObject.PropertyChanged -= __root; });");
            code.AppendLine("}");
            code.AppendLine("// No public overloads here; dispatch occurs in the generic expression-based method.");
            return code.ToString();
        }
        // Declare segment locals for host and target chains (target chain used directly, no rewiring logic yet).
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            var access = BuildChainAccess("fromObject", spec.FromChainSegments.Take(i+1).ToList());
            code.AppendLine($"    {seg.TypeName} __host_{i} = {access};");
        }
        for (int i = 0; i < spec.TargetChainSegments.Count; i++)
        {
            var seg = spec.TargetChainSegments[i];
            var access = BuildChainAccess("targetObject", spec.TargetChainSegments.Take(i+1).ToList());
            code.AppendLine($"    {seg.TypeName} __target_{i} = {access};");
        }
        code.AppendLine("    // Leaf property write cache");
        code.AppendLine("    static System.Reflection.PropertyInfo? __GetLeafWritePI(Type t,string name)=> t.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);");
        code.AppendLine($"    var __leafWrite = __GetLeafWritePI(typeof({spec.TargetChainSegments.Last().DeclaringTypeName}), \"{spec.TargetChainSegments.Last().Name}\");");
        code.AppendLine("    void Push(){ try{ var converted = hostToTargetConv(" + hostLeafAccess + "); " + BuildLeafAssignment("targetObject", spec.TargetChainSegments, "converted") + " } catch { } }");
        // Handlers for host chain segments
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            string nextProp = i + 1 < spec.FromChainSegments.Count ? spec.FromChainSegments[i + 1].Name : spec.FromLeafPropertyName;
            code.AppendLine($"    PropertyChangedEventHandler __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\") {{ RewireHost(); Push(); }} if(e.PropertyName==\"{spec.FromLeafPropertyName}\") Push(); }}; ");
        }
        // Rewire host chain when intermediate replaced
        code.AppendLine("    void RewireHost(){");
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            var access = BuildChainAccess("fromObject", spec.FromChainSegments.Take(i+1).ToList());
            code.AppendLine($"        var new_{i} = {access};");
            code.AppendLine("        if(!ReferenceEquals(__host_"+i+", new_"+i+")) {");
            if (seg.IsNotify)
            {
                code.AppendLine($"            if(__host_{i}!=null) __host_{i}.PropertyChanged -= __h_host_{i};");
            }
            code.AppendLine($"            __host_{i} = new_{i};");
            if (seg.IsNotify)
            {
                code.AppendLine($"            if(__host_{i}!=null) __host_{i}.PropertyChanged += __h_host_{i};");
            }
            code.AppendLine("        }");
        }
        code.AppendLine("    }");
        // Attach initial handlers
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"    if(__host_{i}!=null) __host_{i}.PropertyChanged += __h_host_{i};");
        }
        code.AppendLine("    Push();");
        code.AppendLine("    return Disposable.Create(()=>{");
        for (int i = 0; i < spec.FromChainSegments.Count; i++)
        {
            var seg = spec.FromChainSegments[i];
            if (!seg.IsNotify) continue;
            code.AppendLine($"        if(__host_{i}!=null) __host_{i}.PropertyChanged -= __h_host_{i};");
        }
        code.AppendLine("    });");
        code.AppendLine("}");
        // No public overloads here; dispatch occurs in the generic expression-based method.
        return code.ToString();
    }

    private static string Sanitize(string typeName)
    {
        var sb = new StringBuilder(typeName.Length);
        foreach (var ch in typeName)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else sb.Append('_');
        }
        return sb.ToString();
    }

    private static string GenerateWhen(string name, string iface)
    {
        if (name == "WhenChanged")
        {
            return "public static Observable<TReturn> WhenChanged<TObj,TReturn>(this TObj objectToMonitor, Expression<Func<TObj,TReturn>> propertyExpression, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TObj:INotifyPropertyChanged {\n    if (objectToMonitor == null) return Observable.Empty<TReturn>();\n    var getter = propertyExpression.Compile();\n    return Observable.Create<TReturn>(observer => {\n        List<string> Extract(Expression body){ var list=new List<string>(); while(body is MemberExpression me){ list.Insert(0, me.Member.Name); body=me.Expression;} return list;}\n        var chain = Extract(propertyExpression.Body);\n        var leafName = chain.Count>0 ? chain[chain.Count-1] : null;\n        var nodes = new INotifyPropertyChanged[chain.Count];\n        PropertyChangedEventHandler[] handlers = new PropertyChangedEventHandler[chain.Count];\n        void Detach(int depth){ if(nodes[depth]!=null) nodes[depth].PropertyChanged -= handlers[depth]; nodes[depth]=null;}\n        void Attach(INotifyPropertyChanged obj,int depth){ obj.PropertyChanged += handlers[depth]; }\n        for(int i=0;i<handlers.Length;i++){ int local=i; handlers[i]=(s,e)=>{ if(e.PropertyName==chain[local]){ Wire(); Emit(); } else if(leafName!=null && e.PropertyName==leafName){ Emit(); } }; }\n        void Wire(){ for(int i=0;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); object current=objectToMonitor; for(int depth=0; depth<chain.Count; depth++){ if(current==null) break; if(current is INotifyPropertyChanged npc){ nodes[depth]=npc; Attach(npc, depth);} var prop=current.GetType().GetProperty(chain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); if(prop==null) break; current=prop.GetValue(current); } }\n        void Emit(){ try{ observer.OnNext(getter(objectToMonitor)); } catch{ observer.OnNext(default!);} }\n        PropertyChangedEventHandler rootHandler = null; if (objectToMonitor is INotifyPropertyChanged root && chain.Count>0){ rootHandler = (s,e)=>{ if(e.PropertyName==chain[0]){ Wire(); Emit(); } else if(leafName!=null && e.PropertyName==leafName){ Emit(); } }; root.PropertyChanged += rootHandler; }\n        Wire(); Emit();\n        return Disposable.Create(()=>{ if(rootHandler!=null) ((INotifyPropertyChanged)objectToMonitor).PropertyChanged -= rootHandler; for(int i=0;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); });\n    });\n}";
        }
        else // WhenChanging
        {
            return "public static Observable<TReturn> WhenChanging<TObj,TReturn>(this TObj objectToMonitor, Expression<Func<TObj,TReturn>> propertyExpression, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TObj:INotifyPropertyChanging {\n    if (objectToMonitor == null) return Observable.Empty<TReturn>();\n    var getter = propertyExpression.Compile();\n    return Observable.Create<TReturn>(observer => {\n        List<string> Extract(Expression body){ var list=new List<string>(); while(body is MemberExpression me){ list.Insert(0, me.Member.Name); body=me.Expression;} return list;}\n        var chain = Extract(propertyExpression.Body);\n        var leafName = chain.Count>0 ? chain[chain.Count-1] : null;\n        var nodes = new INotifyPropertyChanging[chain.Count];\n        PropertyChangingEventHandler[] handlers = new PropertyChangingEventHandler[chain.Count];\n        void Detach(int depth){ if(nodes[depth]!=null) nodes[depth].PropertyChanging -= handlers[depth]; nodes[depth]=null;}\n        void Attach(INotifyPropertyChanging obj,int depth){ obj.PropertyChanging += handlers[depth]; }\n        for(int i=0;i<handlers.Length;i++){ int local=i; handlers[i]=(s,e)=>{ if(e.PropertyName==chain[local]){ Wire(); Emit(); } else if(leafName!=null && e.PropertyName==leafName){ Emit(); } }; }\n        void Wire(){ for(int i=0;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); object current=objectToMonitor; for(int depth=0; depth<chain.Count; depth++){ if(current==null) break; if(current is INotifyPropertyChanging npc){ nodes[depth]=npc; Attach(npc, depth);} var prop=current.GetType().GetProperty(chain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); if(prop==null) break; current=prop.GetValue(current); } }\n        void Emit(){ try{ observer.OnNext(getter(objectToMonitor)); } catch{ observer.OnNext(default!);} }\n        PropertyChangingEventHandler rootHandler = null; if (objectToMonitor is INotifyPropertyChanging root && chain.Count>0){ rootHandler = (s,e)=>{ if(e.PropertyName==chain[0]){ Wire(); Emit(); } else if(leafName!=null && e.PropertyName==leafName){ Emit(); } }; root.PropertyChanging += rootHandler; }\n        Wire(); Emit();\n        return Disposable.Create(()=>{ if(rootHandler!=null) ((INotifyPropertyChanging)objectToMonitor).PropertyChanging -= rootHandler; for(int i=0;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); });\n    });\n}";
        }
    }

    private static string GenerateBindOneWayWithDispatch(IReadOnlyList<InvocationModel> specs, bool strictMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("public static IDisposable BindOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty> hostToTargetConv, object scheduler = null, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class {");
        sb.AppendLine("    if(fromObject==null||targetObject==null) return Disposable.Empty;");
        sb.AppendLine("    string __Normalize(string s){ var arr = s.Where(c=>!char.IsWhiteSpace(c)).ToArray(); return new string(arr); };");
        sb.AppendLine("    var __fromText = __Normalize(fromProperty.ToString()); var __toText = __Normalize(toProperty.ToString());");
        sb.AppendLine("    string __ChainKey(Expression expr){ List<string> names=new(); while(expr is MemberExpression me){ names.Insert(0, me.Member.Name); expr=me.Expression; } return string.Join('.', names); };");
        sb.AppendLine("    var __fromChainKey = __ChainKey(fromProperty.Body); var __toChainKey = __ChainKey(toProperty.Body);");
        sb.AppendLine($"    const bool __STRICT = {(strictMode ? "true" : "false")};");
        // Dispatch BEFORE any expression compilation.
        foreach (var spec in specs)
        {
            var methodSafe = "__BindOneWay_" + Sanitize(spec.FromRootType) + "_" + Sanitize(spec.TargetRootType) + "_" + spec.SpecializationHash;
            sb.AppendLine($"    if(__fromChainKey==\"{spec.FromChainKey}\" && __toChainKey==\"{spec.TargetChainKey}\" && fromObject is {spec.FromRootType} && targetObject is {spec.TargetRootType}) return {methodSafe}(({spec.FromRootType})fromObject, ({spec.TargetRootType})targetObject, hostToTargetConv);");
        }
        // Fallback to prior reflective implementation as last resort
        sb.AppendLine("    if(__STRICT) throw new InvalidOperationException(\"Strict binding mode: specialization not found for chain\");");
        // Removed expression compile: use manual reflection walk with shared caches
        sb.AppendLine("    var toMember = (toProperty.Body as MemberExpression)?.Member.Name;");
        sb.AppendLine("    System.Reflection.PropertyInfo? __toLeafPI = toMember!=null ? targetObject.GetType().GetProperty(toMember, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;");
        sb.AppendLine("    List<string> Extract(Expression body){ var list=new List<string>(); while(body is MemberExpression me){ list.Insert(0, me.Member.Name); body=me.Expression;} return list;}");
        sb.AppendLine("    var chain = Extract(fromProperty.Body);");
        sb.AppendLine("    var propCaches = __GetOrAddChainCaches(chain);");
        sb.AppendLine("    var nodes = new INotifyPropertyChanged[chain.Count];");
        sb.AppendLine("    PropertyChangedEventHandler[] handlers = new PropertyChangedEventHandler[chain.Count];");
        sb.AppendLine("    void Detach(int d){ if(nodes[d]!=null) nodes[d].PropertyChanged -= handlers[d]; nodes[d]=null;}");
        sb.AppendLine("    void Attach(INotifyPropertyChanged obj,int d){ obj.PropertyChanged += handlers[d]; }");
        sb.AppendLine("    TFromProperty __GetHostValue(){ object current=fromObject; for(int depth=0; depth<chain.Count; depth++){ if(current==null) return default!; var t=current.GetType(); if(!propCaches[depth].TryGetValue(t,out var pi)){ pi=t.GetProperty(chain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); propCaches[depth][t]=pi; } if(pi==null) return default!; current=pi.GetValue(current); } return current is TFromProperty v ? v : (TFromProperty)current; }");
        sb.AppendLine("    void Update(){ try{ var val=__GetHostValue(); var conv=hostToTargetConv(val); if(__toLeafPI!=null && __toLeafPI.CanWrite) __toLeafPI.SetValue(targetObject,conv); } catch {} }");
        sb.AppendLine("    for(int i=0;i<handlers.Length;i++){ int local=i; handlers[i]=(s,e)=>{ if(e.PropertyName==chain[local]){ RewireFromDepth(local); Update(); } else if(local==chain.Count-1){ Update(); } }; }");
        sb.AppendLine("    void RewireFromDepth(int start){ for(int i=start;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); object current=fromObject; for(int depth=0; depth<chain.Count; depth++){ if(current==null) break; var t=current.GetType(); if(!propCaches[depth].TryGetValue(t, out var prop)){ prop=t.GetProperty(chain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); propCaches[depth][t]=prop; } if(prop==null) break; var value=prop.GetValue(current); if(value is INotifyPropertyChanged npc && depth>=start){ nodes[depth]=npc; Attach(npc, depth);} current=value; } }");
        sb.AppendLine("    void Wire(){ RewireFromDepth(0); }");
        sb.AppendLine("    if(fromObject is INotifyPropertyChanged root && chain.Count>0) root.PropertyChanged += (s,e)=>{ if(e.PropertyName==chain[0]){ RewireFromDepth(0); Update(); } };");
        sb.AppendLine("    Wire(); Update();");
        sb.AppendLine("    return Disposable.Create(()=>{ for(int i=0;i<nodes.Length;i++) if(nodes[i]!=null) Detach(i); });");
        sb.AppendLine("}");
        sb.AppendLine("public static IDisposable BindOneWay<TFrom,TProp,TTarget>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TProp>> fromProperty, Expression<Func<TTarget,TProp>> toProperty, object scheduler = null, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class => BindOneWay(fromObject, targetObject, fromProperty, toProperty, v=>v, scheduler, callerMemberName, callerFilePath, callerLineNumber);");
        return sb.ToString();
    }

    private static string GenerateBindTwoWayWithDispatch(IReadOnlyList<InvocationModel> specs, bool strictMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("public static IDisposable BindTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty> hostToTargetConv, Func<TTargetProperty,TFromProperty> targetToHostConv, object scheduler = null, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class,INotifyPropertyChanged {");
        sb.AppendLine("    if(fromObject==null||targetObject==null) return Disposable.Empty;");
        sb.AppendLine("    string __Normalize(string s){ var arr = s.Where(c=>!char.IsWhiteSpace(c)).ToArray(); return new string(arr); };");
        sb.AppendLine("    var __fromText = __Normalize(fromProperty.ToString()); var __toText = __Normalize(toProperty.ToString());");
        sb.AppendLine("    string __ChainKey(Expression expr){ List<string> names=new(); while(expr is MemberExpression me){ names.Insert(0, me.Member.Name); expr=me.Expression; } return string.Join('.', names); };");
        sb.AppendLine("    var __fromChainKey = __ChainKey(fromProperty.Body); var __toChainKey = __ChainKey(toProperty.Body);");
        sb.AppendLine($"    const bool __STRICT = {(strictMode ? "true" : "false")};");
        // Dispatch BEFORE expression compilation.
        foreach (var spec in specs)
        {
            var methodSafe = "__BindTwoWay_" + Sanitize(spec.FromRootType) + "_" + Sanitize(spec.TargetRootType) + "_" + spec.SpecializationHash;
            sb.AppendLine($"    if(__fromChainKey==\"{spec.FromChainKey}\" && __toChainKey==\"{spec.TargetChainKey}\" && fromObject is {spec.FromRootType} && targetObject is {spec.TargetRootType}) return {methodSafe}(({spec.FromRootType})fromObject, ({spec.TargetRootType})targetObject, hostToTargetConv, targetToHostConv);");
        }
        sb.AppendLine("    if(__STRICT) throw new InvalidOperationException(\"Strict binding mode: specialization not found for chain\");");
        // Remove expression compile; use reflection chain traversal
        sb.AppendLine("    var fromMember = (fromProperty.Body as MemberExpression)?.Member.Name;");
        sb.AppendLine("    var toMember = (toProperty.Body as MemberExpression)?.Member.Name;");
        sb.AppendLine("    System.Reflection.PropertyInfo? __fromLeafPI = fromMember!=null ? fromObject.GetType().GetProperty(fromMember, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;");
        sb.AppendLine("    System.Reflection.PropertyInfo? __toLeafPI = toMember!=null ? targetObject.GetType().GetProperty(toMember, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;");
        sb.AppendLine("    List<string> Extract(Expression b){ var list=new List<string>(); while(b is MemberExpression me){ list.Insert(0, me.Member.Name); b=me.Expression;} return list;}");
        sb.AppendLine("    var fromChain = Extract(fromProperty.Body);");
        sb.AppendLine("    var toChain = Extract(toProperty.Body);");
        sb.AppendLine("    var fromPropCaches = __GetOrAddChainCaches(fromChain);");
        sb.AppendLine("    var toPropCaches = __GetOrAddChainCaches(toChain);");
        sb.AppendLine("    var fromNodes = new INotifyPropertyChanged[fromChain.Count];");
        sb.AppendLine("    var toNodes = new INotifyPropertyChanged[toChain.Count];");
        sb.AppendLine("    PropertyChangedEventHandler[] fromHandlers = new PropertyChangedEventHandler[fromChain.Count];");
        sb.AppendLine("    PropertyChangedEventHandler[] toHandlers = new PropertyChangedEventHandler[toChain.Count];");
        sb.AppendLine("    bool _updating=false;");
        sb.AppendLine("    TFromProperty __GetHostValue(){ object current=fromObject; for(int depth=0; depth<fromChain.Count; depth++){ if(current==null) return default!; var t=current.GetType(); if(!fromPropCaches[depth].TryGetValue(t,out var pi)){ pi=t.GetProperty(fromChain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); fromPropCaches[depth][t]=pi; } if(pi==null) return default!; current=pi.GetValue(current); } return current is TFromProperty hv ? hv : (TFromProperty)current; }");
        sb.AppendLine("    TTargetProperty __GetTargetValue(){ object current=targetObject; for(int depth=0; depth<toChain.Count; depth++){ if(current==null) return default!; var t=current.GetType(); if(!toPropCaches[depth].TryGetValue(t,out var pi)){ pi=t.GetProperty(toChain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); toPropCaches[depth][t]=pi; } if(pi==null) return default!; current=pi.GetValue(current); } return current is TTargetProperty tv ? tv : (TTargetProperty)current; }");
        sb.AppendLine("    void DetachFrom(int d){ if(fromNodes[d]!=null) fromNodes[d].PropertyChanged -= fromHandlers[d]; fromNodes[d]=null;}");
        sb.AppendLine("    void DetachTo(int d){ if(toNodes[d]!=null) toNodes[d].PropertyChanged -= toHandlers[d]; toNodes[d]=null;}");
        sb.AppendLine("    void AttachFrom(INotifyPropertyChanged obj,int d){ obj.PropertyChanged += fromHandlers[d]; }");
        sb.AppendLine("    void AttachTo(INotifyPropertyChanged obj,int d){ obj.PropertyChanged += toHandlers[d]; }");
        sb.AppendLine("    void UpdateTarget(){ if(_updating) return; try{ _updating=true; var v=__GetHostValue(); var conv=hostToTargetConv(v); if(__toLeafPI!=null && __toLeafPI.CanWrite) __toLeafPI.SetValue(targetObject,conv); \n#if R3EXT_TRACE\n R3ExtGeneratedInstrumentation.BindUpdates++;\n#endif\n } catch {} finally { _updating=false; } }");
        sb.AppendLine("    void UpdateHost(){ if(_updating) return; try{ _updating=true; var v=__GetTargetValue(); var conv=targetToHostConv(v); if(__fromLeafPI!=null && __fromLeafPI.CanWrite) __fromLeafPI.SetValue(fromObject,conv); \n#if R3EXT_TRACE\n R3ExtGeneratedInstrumentation.BindUpdates++;\n#endif\n } catch {} finally { _updating=false; } }");
        sb.AppendLine("    for(int i=0;i<fromHandlers.Length;i++){ int local=i; fromHandlers[i]=(s,e)=>{ if(e.PropertyName==fromChain[local]){ RewireFromDepth(local); UpdateTarget(); } else if(local==fromChain.Count-1){ UpdateTarget(); } }; }");
        sb.AppendLine("    for(int i=0;i<toHandlers.Length;i++){ int local=i; toHandlers[i]=(s,e)=>{ if(e.PropertyName==toChain[local]){ RewireToDepth(local); UpdateHost(); } else if(local==toChain.Count-1){ UpdateHost(); } }; }");
        sb.AppendLine("    void RewireFromDepth(int start){ for(int i=start;i<fromNodes.Length;i++) if(fromNodes[i]!=null) DetachFrom(i); object current=fromObject; for(int depth=0; depth<fromChain.Count; depth++){ if(current==null) break; var t=current.GetType(); if(!fromPropCaches[depth].TryGetValue(t, out var prop)){ prop=t.GetProperty(fromChain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); fromPropCaches[depth][t]=prop; } if(prop==null) break; var value=prop.GetValue(current); if(value is INotifyPropertyChanged npc && depth>=start){ fromNodes[depth]=npc; AttachFrom(npc, depth);} current=value; }\n#if R3EXT_TRACE\n R3ExtGeneratedInstrumentation.NotifyWires++;\n#endif\n }");
        sb.AppendLine("    void RewireToDepth(int start){ for(int i=start;i<toNodes.Length;i++) if(toNodes[i]!=null) DetachTo(i); object current=targetObject; for(int depth=0; depth<toChain.Count; depth++){ if(current==null) break; var t=current.GetType(); if(!toPropCaches[depth].TryGetValue(t, out var prop)){ prop=t.GetProperty(toChain[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); toPropCaches[depth][t]=prop; } if(prop==null) break; var value=prop.GetValue(current); if(value is INotifyPropertyChanged npc && depth>=start){ toNodes[depth]=npc; AttachTo(npc, depth);} current=value; }\n#if R3EXT_TRACE\n R3ExtGeneratedInstrumentation.NotifyWires++;\n#endif\n }");
        sb.AppendLine("    void WireFrom(){ RewireFromDepth(0); }");
        sb.AppendLine("    void WireTo(){ RewireToDepth(0); }");
        sb.AppendLine("    if(fromObject is INotifyPropertyChanged rootFrom && fromChain.Count>0) rootFrom.PropertyChanged += (s,e)=>{ if(e.PropertyName==fromChain[0]){ WireFrom(); UpdateTarget(); } };");
        sb.AppendLine("    if(targetObject is INotifyPropertyChanged rootTo && toChain.Count>0) rootTo.PropertyChanged += (s,e)=>{ if(e.PropertyName==toChain[0]){ WireTo(); UpdateHost(); } };");
        sb.AppendLine("    WireFrom(); WireTo(); UpdateTarget();");
        sb.AppendLine("    return Disposable.Create(()=>{ for(int i=0;i<fromNodes.Length;i++) if(fromNodes[i]!=null) DetachFrom(i); for(int i=0;i<toNodes.Length;i++) if(toNodes[i]!=null) DetachTo(i); });");
        sb.AppendLine("}");
        sb.AppendLine("public static IDisposable BindTwoWay<TFrom,TProp,TTarget>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TProp>> fromProperty, Expression<Func<TTarget,TProp>> toProperty, object scheduler = null, string callerMemberName = null, string callerFilePath = null, int callerLineNumber = 0) where TFrom:class,INotifyPropertyChanged where TTarget:class,INotifyPropertyChanged => BindTwoWay(fromObject, targetObject, fromProperty, toProperty, v=>v, v=>v, scheduler, callerMemberName, callerFilePath, callerLineNumber);");
        return sb.ToString();
    }

    private sealed class InvocationModel
    {
        public string MethodKind { get; set; } = string.Empty;
        public bool CanSpecialize { get; set; }
        public string FromRootType { get; set; } = string.Empty;
        public string TargetRootType { get; set; } = string.Empty;
        public string FromLeafPropertyName { get; set; } = string.Empty;
        public string TargetLeafPropertyName { get; set; } = string.Empty;
        public string FromLeafPropertyType { get; set; } = string.Empty;
        public string TargetLeafPropertyType { get; set; } = string.Empty;
        public string FromLambdaText { get; set; } = string.Empty;
        public string ToLambdaText { get; set; } = string.Empty;
        public string FromLambdaNormalized { get; set; } = string.Empty;
        public string ToLambdaNormalized { get; set; } = string.Empty;
        public string FromChainKey { get; set; } = string.Empty; // dot-separated property names
        public string TargetChainKey { get; set; } = string.Empty;
        public List<PropertySegment> FromChainSegments { get; } = new();
        public List<PropertySegment> TargetChainSegments { get; } = new();
        public string SpecializationHash => (FromRootType + "|" + TargetRootType + "|" + string.Join(".", FromChainSegments.Select(x=>x.Name)) + "|" + string.Join(".", TargetChainSegments.Select(x=>x.Name))).GetHashCode().ToString();
        public string SpecializationKey => FromRootType + "|" + TargetRootType + "|" + FromLeafPropertyName + "|" + TargetLeafPropertyName + "|" + FromLeafPropertyType + "|" + TargetLeafPropertyType;
        public Location? Location { get; set; }
        public string? UnsupportedReason { get; set; }
    }

    private sealed class PropertySegment
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsNotify { get; set; }
        public bool IsNonPublic { get; set; }
        public bool HasSetter { get; set; }
        public string DeclaringTypeName { get; set; } = string.Empty;
    }

    private static string BuildChainAccess(string rootName, List<PropertySegment> segments)
    {
        if (segments.Count == 0) return rootName;
        var sb = new StringBuilder(rootName);
        foreach (var seg in segments)
        {
            sb.Append('.').Append(seg.Name);
        }
        return sb.ToString();
    }

    // Null-safe chain access with UnsafeAccessor usage for non-public members.
    private static string BuildChainAccessUnsafe(string rootName, List<PropertySegment> segments)
    {
        if (segments.Count == 0) return rootName;
        string currentExpr = rootName;
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var safeDecl = Sanitize(seg.DeclaringTypeName);
            var safeProp = Sanitize(seg.Name);
            string accessPart;
            if (seg.IsNonPublic)
            {
                accessPart = $"(( {currentExpr} )==null ? default({seg.TypeName}) : __UA_GET_{safeDecl}_{safeProp}({currentExpr}))";
            }
            else
            {
                accessPart = $"(( {currentExpr} )==null ? default({seg.TypeName}) : {currentExpr}.{seg.Name})";
            }
            currentExpr = accessPart;
        }
        return currentExpr;
    }

    private static string BuildLeafAssignment(string rootName, List<PropertySegment> segments, string valueExpression)
    {
        if (segments.Count == 0) return ""; // nothing to assign
        var leaf = segments.Last();
        var parentSegments = segments.Take(segments.Count - 1).ToList();
        string parentExpr = rootName;
        for (int i = 0; i < parentSegments.Count; i++)
        {
            var seg = parentSegments[i];
            var safeDecl = Sanitize(seg.DeclaringTypeName);
            var safeProp = Sanitize(seg.Name);
            if (seg.IsNonPublic)
            {
                parentExpr = $"(( {parentExpr} )==null ? default({seg.TypeName}) : __UA_GET_{safeDecl}_{safeProp}({parentExpr}))";
            }
            else
            {
                parentExpr = $"(( {parentExpr} )==null ? default({seg.TypeName}) : {parentExpr}.{seg.Name})";
            }
        }
        // If parentExpr could be default (null), guard assignment.
        var safeLeafDecl = Sanitize(leaf.DeclaringTypeName);
        var safeLeafProp = Sanitize(leaf.Name);
        if (leaf.IsNonPublic)
        {
            if (!leaf.HasSetter) return ""; // cannot assign
            return $"if(({parentExpr})!=null) __UA_SET_{safeLeafDecl}_{safeLeafProp}({parentExpr}, {valueExpression});";
        }
        else
        {
            return $"if(({parentExpr})!=null) {parentExpr}.{leaf.Name} = {valueExpression};";
        }
    }
}
