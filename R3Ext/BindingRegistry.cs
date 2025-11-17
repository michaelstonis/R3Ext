using System;
using System.Collections.Generic;
using R3;

namespace R3Ext;

// Internal registry used by generated extension methods to allow cross-assembly registration.
// Currently only stubbed; referencing assemblies will later register factories via module initializers.
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
    public static void RegisterOneWay<TFrom,TFromProp,TTarget,TTargetProp>(string fromPath, string toPath, Func<TFrom, TTarget, Func<TFromProp,TTargetProp>?, IDisposable> factory)
    {
        var key = fromPath + "|" + toPath;
        Console.WriteLine($"[BindingRegistry] RegisterOneWay {key} as {typeof(TFrom).Name}->{typeof(TTarget).Name}");
        if (!_oneWay.TryGetValue(key, out var list))
        {
            list = new List<OneWayEntry>();
            _oneWay[key] = list;
        }
        list.Add(new OneWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f,t,conv) => factory((TFrom)f, (TTarget)t, (Func<TFromProp,TTargetProp>?)conv)
        });
    }
    public static void RegisterTwoWay<TFrom,TFromProp,TTarget,TTargetProp>(string fromPath, string toPath, Func<TFrom, TTarget, Func<TFromProp,TTargetProp>?, Func<TTargetProp,TFromProp>?, IDisposable> factory)
    {
        var key = fromPath + "|" + toPath;
        Console.WriteLine($"[BindingRegistry] RegisterTwoWay {key} as {typeof(TFrom).Name}<->{typeof(TTarget).Name}");
        if (!_twoWay.TryGetValue(key, out var list))
        {
            list = new List<TwoWayEntry>();
            _twoWay[key] = list;
        }
        list.Add(new TwoWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f,t,ht,th) => factory((TFrom)f, (TTarget)t, (Func<TFromProp,TTargetProp>?)ht, (Func<TTargetProp,TFromProp>?)th)
        });
    }
    public static void RegisterWhenChanged<TObj,TReturn>(string whenPath, Func<TObj, Observable<TReturn>> factory)
    {
        // whenPath is usually "TypeSimpleName|lambdaText" per generator. Index by path after the first '|'.
        var (typePart, pathPart) = SplitTypePath(whenPath);
        Console.WriteLine($"[BindingRegistry] RegisterWhenChanged {typePart}|{pathPart} as {typeof(TObj).Name}");
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
    public static bool TryCreateOneWay<TFrom,TFromProp,TTarget,TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj, Func<TFromProp,TTargetProp>? conv, out IDisposable disposable)
    {
        var key = fromPath + "|" + toPath;
        var fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        var targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Console.WriteLine($"[BindingRegistry] TryCreateOneWay lookup {key} for {fromRuntime.Name}->{targetRuntime.Name}");
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

    public static bool TryCreateTwoWay<TFrom,TFromProp,TTarget,TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj, Func<TFromProp,TTargetProp>? hostToTarget, Func<TTargetProp,TFromProp>? targetToHost, out IDisposable disposable)
    {
        var key = fromPath + "|" + toPath;
        var fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        var targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Console.WriteLine($"[BindingRegistry] TryCreateTwoWay lookup {key} for {fromRuntime.Name}<->{targetRuntime.Name}");
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

    public static bool TryCreateWhenChanged<TObj,TReturn>(string whenPath, TObj obj, out Observable<TReturn> observable)
    {
        var (typePart, pathPart) = SplitTypePath(whenPath);
        var objRuntime = obj?.GetType() ?? typeof(TObj);
        Console.WriteLine($"[BindingRegistry] TryCreateWhenChanged lookup {typePart}|{pathPart} for {objRuntime.Name}");
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
        observable = default!;
        return false;
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
}
