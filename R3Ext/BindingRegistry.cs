using System;
using System.Collections.Generic;
using System.ComponentModel;
using R3;

namespace R3Ext;

// Internal registry used by generated extension methods to allow cross-assembly registration.
// Currently only stubbed; referencing assemblies will later register factories via module initializers.
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BindingRegistry
{
    // For bindings, index by "fromPath|toPath"; each key can have multiple
    // candidates for different (from/target) type combinations. At lookup we
    // choose the most specific candidate compatible with runtime types.
    private sealed class OneWayEntry
    {
        public required Type FromType { get; init; }
        public required Type TargetType { get; init; }
        public required Func<object, object, object?, IDisposable> Factory { get; init; }
        public override string ToString() => $"OneWayEntry[{FromType.Name}->{TargetType.Name}]";
    }

    private sealed class TwoWayEntry
    {
        public required Type FromType { get; init; }
        public required Type TargetType { get; init; }
        public required Func<object, object, object?, object?, IDisposable> Factory { get; init; }
        public override string ToString() => $"TwoWayEntry[{FromType.Name}<->{TargetType.Name}]";
    }

    // For WhenChanged, index by path only (after the first '|'). Multiple
    // candidates can exist for different object types.
    private sealed class WhenEntry
    {
        public required Type ObjType { get; init; }
        public required Func<object, Observable<object>> Factory { get; init; }
        public override string ToString() => $"WhenEntry[{ObjType.Name}]";
    }

    private static readonly Dictionary<string, List<OneWayEntry>> _oneWay = new();
    private static readonly Dictionary<string, List<TwoWayEntry>> _twoWay = new();
    private static readonly Dictionary<string, List<WhenEntry>> _whenChanged = new();

    // Registration API (called from generated module initializers in referencing assemblies)
    public static void RegisterOneWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, Func<TFrom, TTarget, Func<TFromProp, TTargetProp>?, IDisposable> factory)
    {
        var key = fromPath + "|" + toPath;
        Log($"[BindingRegistry] RegisterOneWay {key} as {typeof(TFrom).Name}->{typeof(TTarget).Name}");
        if (!_oneWay.TryGetValue(key, out var list))
        {
            list = new List<OneWayEntry>();
            _oneWay[key] = list;
        }
        list.Add(new OneWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f, t, conv) => factory((TFrom)f, (TTarget)t, (Func<TFromProp, TTargetProp>?)conv)
        });
    }
    public static void RegisterTwoWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, Func<TFrom, TTarget, Func<TFromProp, TTargetProp>?, Func<TTargetProp, TFromProp>?, IDisposable> factory)
    {
        var key = fromPath + "|" + toPath;
        Log($"[BindingRegistry] RegisterTwoWay {key} as {typeof(TFrom).Name}<->{typeof(TTarget).Name}");
        if (!_twoWay.TryGetValue(key, out var list))
        {
            list = new List<TwoWayEntry>();
            _twoWay[key] = list;
        }
        list.Add(new TwoWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f, t, ht, th) => factory((TFrom)f, (TTarget)t, (Func<TFromProp, TTargetProp>?)ht, (Func<TTargetProp, TFromProp>?)th)
        });
    }
    public static void RegisterWhenChanged<TObj, TReturn>(string whenPath, Func<TObj, Observable<TReturn>> factory)
    {
        // whenPath is usually "TypeSimpleName|lambdaText" per generator. Index by path after the first '|'.
        var (typePart, pathPart) = SplitTypePath(whenPath);
        Log($"[BindingRegistry] RegisterWhenChanged {typePart}|{pathPart} as {typeof(TObj).Name}");
        if (!_whenChanged.TryGetValue(pathPart, out var list))
        {
            list = new List<WhenEntry>();
            _whenChanged[pathPart] = list;
        }
        list.Add(new WhenEntry
        {
            ObjType = typeof(TObj),
            Factory = o => factory((TObj)o!).Select(v => (object?)v!)
        });
    }

    // TryCreate APIs used by generated extension methods before falling back to specialized implementations
    public static bool TryCreateOneWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj, Func<TFromProp, TTargetProp>? conv, out IDisposable disposable)
    {
        var key = fromPath + "|" + toPath;
        var fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        var targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Log($"[BindingRegistry] TryCreateOneWay lookup {key} for {fromRuntime.Name}->{targetRuntime.Name}");
        if (_oneWay.TryGetValue(key, out var list))
        {
            var entry = PickBest(list, fromRuntime, targetRuntime);
            if (entry is not null)
            {
                disposable = entry.Factory(fromObj!, targetObj!, conv);
                return true;
            }
        }
        disposable = default!;
        return false;
    }

    public static bool TryCreateTwoWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj, Func<TFromProp, TTargetProp>? hostToTarget, Func<TTargetProp, TFromProp>? targetToHost, out IDisposable disposable)
    {
        var key = fromPath + "|" + toPath;
        var fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        var targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Log($"[BindingRegistry] TryCreateTwoWay lookup {key} for {fromRuntime.Name}<->{targetRuntime.Name}");
        if (_twoWay.TryGetValue(key, out var list))
        {
            var entry = PickBest(list, fromRuntime, targetRuntime);
            if (entry is not null)
            {
                disposable = entry.Factory(fromObj!, targetObj!, hostToTarget, targetToHost);
                return true;
            }
        }
        disposable = default!;
        return false;
    }

    public static bool TryCreateWhenChanged<TObj, TReturn>(string whenPath, TObj obj, out Observable<TReturn> observable)
    {
        var (typePart, pathPart) = SplitTypePath(whenPath);
        var objRuntime = obj?.GetType() ?? typeof(TObj);
        Log($"[BindingRegistry] TryCreateWhenChanged lookup {typePart}|{pathPart} for {objRuntime.Name}");
        if (_whenChanged.TryGetValue(pathPart, out var list))
        {
            var entry = PickBest(list, objRuntime);
            if (entry is not null)
            {
                var raw = entry.Factory(obj!);
                observable = raw.Select(v => (TReturn)v!);
                return true;
            }
        }
        // Fallback: construct reflective chain watcher when no generated entry exists.
        // Parse lambda text: expected format "param => param.Prop1.Prop2.Leaf".
        try
        {
            var arrowIdx = pathPart.IndexOf("=>", StringComparison.Ordinal);
            string chainExpr = arrowIdx >= 0 ? pathPart.Substring(arrowIdx + 2).Trim() : pathPart.Trim();
            // Remove leading parameter name (before first '.')
            var firstDot = chainExpr.IndexOf('.') ;
            if (firstDot >= 0) chainExpr = chainExpr.Substring(firstDot + 1); // drop root param identifier
            var segments = chainExpr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0 || obj is null || obj is not INotifyPropertyChanged npcRoot)
            {
                observable = Observable.Empty<TReturn>();
                return true;
            }
            observable = Observable.Create<TReturn>(observer =>
            {
                // Holds current chain objects implementing INotifyPropertyChanged
                var notifyNodes = new INotifyPropertyChanged[segments.Length];
                PropertyChangedEventHandler[] handlers = new PropertyChangedEventHandler[segments.Length];
                void Detach(int depth)
                {
                    if (notifyNodes[depth] != null)
                        notifyNodes[depth]!.PropertyChanged -= handlers[depth];
                    notifyNodes[depth] = null!;
                }
                for (int i = 0; i < handlers.Length; i++)
                {
                    int local = i;
                    handlers[i] = (s, e) =>
                    {
                        // If intermediate segment replaced, rewire from that depth
                        if (e.PropertyName == segments[local])
                        {
                            RewireFrom(local);
                            Emit();
                        }
                        else if (e.PropertyName == segments[^1])
                        {
                            // Leaf changed (could be same event as intermediate replacement)
                            Emit();
                        }
                    };
                }
                void RewireFrom(int startDepth)
                {
                    // Detach downstream handlers
                    for (int d = startDepth; d < segments.Length; d++)
                    {
                        if (notifyNodes[d] != null) Detach(d);
                    }
                    object? current = obj;
                    for (int depth = 0; depth < segments.Length; depth++)
                    {
                        if (current == null) break;
                        // Owner object for property segments[depth] is current before accessing the property value.
                        if (depth >= startDepth && current is INotifyPropertyChanged owner)
                        {
                            notifyNodes[depth] = owner;
                            owner.PropertyChanged += handlers[depth];
                        }
                        var propName = segments[depth];
                        var prop = current.GetType().GetProperty(propName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (prop == null) break;
                        current = prop.GetValue(current);
                    }
                }
                void WireAll()
                {
                    RewireFrom(0);
                }
                void Emit()
                {
                    try
                    {
                        object? current = obj;
                        for (int depth = 0; depth < segments.Length; depth++)
                        {
                            if (current == null) break;
                            var prop = current.GetType().GetProperty(segments[depth], System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            if (prop == null) { current = null; break; }
                            current = prop.GetValue(current);
                        }
                        if (current is TReturn typed)
                        {
                            observer.OnNext(typed);
                        }
                        else
                        {
                            observer.OnNext(default!);
                        }
                    }
                    catch
                    {
                        observer.OnNext(default!);
                    }
                }
                // Root handler for first segment (Mid in example) to catch root-level replacement
                PropertyChangedEventHandler? rootHandler = null;
                if (npcRoot != null && segments.Length > 0)
                {
                    rootHandler = (s, e) =>
                    {
                        if (e.PropertyName == segments[0])
                        {
                            RewireFrom(0);
                            Emit();
                        }
                        else if (e.PropertyName == segments[^1])
                        {
                            Emit();
                        }
                    };
                    npcRoot.PropertyChanged += rootHandler;
                }
                WireAll();
                Emit();
                return Disposable.Create(() =>
                {
                    if (rootHandler != null) npcRoot.PropertyChanged -= rootHandler;
                    for (int i = 0; i < notifyNodes.Length; i++)
                    {
                        if (notifyNodes[i] != null) Detach(i);
                    }
                });
            });
            return true;
        }
        catch
        {
            observable = Observable.Empty<TReturn>();
            return true; // Provide empty fallback rather than failure.
        }
    }

    private static OneWayEntry? PickBest(List<OneWayEntry> list, Type fromRuntime, Type targetRuntime)
    {
        OneWayEntry? best = null;
        var bestScore = int.MaxValue;
        foreach (var e in list)
        {
            if (!e.FromType.IsAssignableFrom(fromRuntime) || !e.TargetType.IsAssignableFrom(targetRuntime)) continue;
            var score = Distance(e.FromType, fromRuntime) + Distance(e.TargetType, targetRuntime);
            if (score < bestScore)
            {
                best = e;
                bestScore = score;
            }
        }
        return best;
    }

    private static TwoWayEntry? PickBest(List<TwoWayEntry> list, Type fromRuntime, Type targetRuntime)
    {
        TwoWayEntry? best = null;
        var bestScore = int.MaxValue;
        foreach (var e in list)
        {
            if (!e.FromType.IsAssignableFrom(fromRuntime) || !e.TargetType.IsAssignableFrom(targetRuntime)) continue;
            var score = Distance(e.FromType, fromRuntime) + Distance(e.TargetType, targetRuntime);
            if (score < bestScore)
            {
                best = e;
                bestScore = score;
            }
        }
        return best;
    }

    private static WhenEntry? PickBest(List<WhenEntry> list, Type objRuntime)
    {
        WhenEntry? best = null;
        var bestScore = int.MaxValue;
        foreach (var e in list)
        {
            if (!e.ObjType.IsAssignableFrom(objRuntime)) continue;
            var score = Distance(e.ObjType, objRuntime);
            if (score < bestScore)
            {
                best = e;
                bestScore = score;
            }
        }
        return best;
    }

    private static int Distance(Type baseType, Type runtimeType)
    {
        if (baseType == runtimeType) return 0;
        if (!baseType.IsAssignableFrom(runtimeType)) return int.MaxValue / 2;
        // If baseType is interface, use small constant distance to favor more specific classes
        if (baseType.IsInterface) return 1;
        var d = 0;
        for (var t = runtimeType; t is not null && baseType.IsAssignableFrom(t); t = t.BaseType)
        {
            if (t == baseType) return d;
            d++;
        }
        return d;
    }

    private static (string typePart, string pathPart) SplitTypePath(string whenPath)
    {
        var idx = whenPath.IndexOf('|');
        if (idx <= 0 || idx >= whenPath.Length - 1) return ("", whenPath);
        return (whenPath.Substring(0, idx), whenPath.Substring(idx + 1));
    }

    private static void Log(string message)
    {
        #if DEBUG
        Console.WriteLine(message);
        #endif
    }
}
