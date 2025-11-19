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

        public override string ToString()
        {
            return $"OneWayEntry[{FromType.Name}->{TargetType.Name}]";
        }
    }

    private sealed class TwoWayEntry
    {
        public required Type FromType { get; init; }

        public required Type TargetType { get; init; }

        public required Func<object, object, object?, object?, IDisposable> Factory { get; init; }

        public override string ToString()
        {
            return $"TwoWayEntry[{FromType.Name}<->{TargetType.Name}]";
        }
    }

    // For WhenChanged, index by path only (after the first '|'). Multiple
    // candidates can exist for different object types.
    private sealed class WhenEntry
    {
        public required Type ObjType { get; init; }

        public required Func<object, Observable<object>> Factory { get; init; }

        public override string ToString()
        {
            return $"WhenEntry[{ObjType.Name}]";
        }
    }

    private static readonly Dictionary<string, List<OneWayEntry>> _oneWay = new();
    private static readonly Dictionary<string, List<TwoWayEntry>> _twoWay = new();
    private static readonly Dictionary<string, List<WhenEntry>> _whenChanged = new();

    // Registration API (called from generated module initializers in referencing assemblies)
    public static void RegisterOneWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath,
        Func<TFrom, TTarget, Func<TFromProp, TTargetProp>?, IDisposable> factory)
    {
        string key = fromPath + "|" + toPath;
        Log($"[BindingRegistry] RegisterOneWay {key} as {typeof(TFrom).Name}->{typeof(TTarget).Name}");
        if (!_oneWay.TryGetValue(key, out List<OneWayEntry>? list))
        {
            list = new List<OneWayEntry>();
            _oneWay[key] = list;
        }

        list.Add(new OneWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f, t, conv) => factory((TFrom)f, (TTarget)t, (Func<TFromProp, TTargetProp>?)conv),
        });
    }

    public static void RegisterTwoWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath,
        Func<TFrom, TTarget, Func<TFromProp, TTargetProp>?, Func<TTargetProp, TFromProp>?, IDisposable> factory)
    {
        string key = fromPath + "|" + toPath;
        Log($"[BindingRegistry] RegisterTwoWay {key} as {typeof(TFrom).Name}<->{typeof(TTarget).Name}");
        if (!_twoWay.TryGetValue(key, out List<TwoWayEntry>? list))
        {
            list = new List<TwoWayEntry>();
            _twoWay[key] = list;
        }

        list.Add(new TwoWayEntry
        {
            FromType = typeof(TFrom),
            TargetType = typeof(TTarget),
            Factory = (f, t, ht, th) => factory((TFrom)f, (TTarget)t, (Func<TFromProp, TTargetProp>?)ht, (Func<TTargetProp, TFromProp>?)th),
        });
    }

    public static void RegisterWhenChanged<TObj, TReturn>(string whenPath, Func<TObj, Observable<TReturn>> factory)
    {
        // whenPath is usually "TypeSimpleName|lambdaText" per generator. Index by path after the first '|'.
        (string typePart, string pathPart) = SplitTypePath(whenPath);
        Log($"[BindingRegistry] RegisterWhenChanged {typePart}|{pathPart} as {typeof(TObj).Name}");
        if (!_whenChanged.TryGetValue(pathPart, out List<WhenEntry>? list))
        {
            list = new List<WhenEntry>();
            _whenChanged[pathPart] = list;
        }

        list.Add(new WhenEntry { ObjType = typeof(TObj), Factory = o => factory((TObj)o!).Select(v => (object?)v!), });
    }

    // TryCreate APIs used by generated extension methods before falling back to specialized implementations
    public static bool TryCreateOneWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj,
        Func<TFromProp, TTargetProp>? conv, out IDisposable disposable)
    {
        string key = fromPath + "|" + toPath;
        Type fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        Type targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Log($"[BindingRegistry] TryCreateOneWay lookup {key} for {fromRuntime.Name}->{targetRuntime.Name}");
        if (_oneWay.TryGetValue(key, out List<OneWayEntry>? list))
        {
            OneWayEntry? entry = PickBest(list, fromRuntime, targetRuntime);
            if (entry is not null)
            {
                disposable = entry.Factory(fromObj!, targetObj!, conv);
                return true;
            }
        }

        disposable = default!;
        return false;
    }

    public static bool TryCreateTwoWay<TFrom, TFromProp, TTarget, TTargetProp>(string fromPath, string toPath, TFrom fromObj, TTarget targetObj,
        Func<TFromProp, TTargetProp>? hostToTarget, Func<TTargetProp, TFromProp>? targetToHost, out IDisposable disposable)
    {
        string key = fromPath + "|" + toPath;
        Type fromRuntime = fromObj?.GetType() ?? typeof(TFrom);
        Type targetRuntime = targetObj?.GetType() ?? typeof(TTarget);
        Log($"[BindingRegistry] TryCreateTwoWay lookup {key} for {fromRuntime.Name}<->{targetRuntime.Name}");
        if (_twoWay.TryGetValue(key, out List<TwoWayEntry>? list))
        {
            TwoWayEntry? entry = PickBest(list, fromRuntime, targetRuntime);
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
        (string typePart, string pathPart) = SplitTypePath(whenPath);
        Type objRuntime = obj?.GetType() ?? typeof(TObj);
        Log($"[BindingRegistry] TryCreateWhenChanged lookup {typePart}|{pathPart} for {objRuntime.Name}");
        if (_whenChanged.TryGetValue(pathPart, out List<WhenEntry>? list))
        {
            WhenEntry? entry = PickBest(list, objRuntime);
            if (entry is not null)
            {
                Observable<object> raw = entry.Factory(obj!);
                observable = raw.Select(v => (TReturn)v!);
                return true;
            }
        }

        // No generated binding found - fail fast for AOT compatibility
        observable = default!;
        return false;
    }

    private static OneWayEntry? PickBest(List<OneWayEntry> list, Type fromRuntime, Type targetRuntime)
    {
        OneWayEntry? best = null;
        int bestScore = int.MaxValue;
        foreach (OneWayEntry e in list)
        {
            if (!e.FromType.IsAssignableFrom(fromRuntime) || !e.TargetType.IsAssignableFrom(targetRuntime))
            {
                continue;
            }

            int score = Distance(e.FromType, fromRuntime) + Distance(e.TargetType, targetRuntime);
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
        int bestScore = int.MaxValue;
        foreach (TwoWayEntry e in list)
        {
            if (!e.FromType.IsAssignableFrom(fromRuntime) || !e.TargetType.IsAssignableFrom(targetRuntime))
            {
                continue;
            }

            int score = Distance(e.FromType, fromRuntime) + Distance(e.TargetType, targetRuntime);
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
        int bestScore = int.MaxValue;
        foreach (WhenEntry e in list)
        {
            if (!e.ObjType.IsAssignableFrom(objRuntime))
            {
                continue;
            }

            int score = Distance(e.ObjType, objRuntime);
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
        if (baseType == runtimeType)
        {
            return 0;
        }

        if (!baseType.IsAssignableFrom(runtimeType))
        {
            return int.MaxValue / 2;
        }

        // If baseType is interface, use small constant distance to favor more specific classes
        if (baseType.IsInterface)
        {
            return 1;
        }

        int d = 0;
        for (Type? t = runtimeType; t is not null && baseType.IsAssignableFrom(t); t = t.BaseType)
        {
            if (t == baseType)
            {
                return d;
            }

            d++;
        }

        return d;
    }

    private static (string typePart, string pathPart) SplitTypePath(string whenPath)
    {
        int idx = whenPath.IndexOf('|');
        if (idx <= 0 || idx >= whenPath.Length - 1)
        {
            return (string.Empty, whenPath);
        }

        return (whenPath.Substring(0, idx), whenPath.Substring(idx + 1));
    }

    private static void Log(string message)
    {
#if DEBUG
        Console.WriteLine(message);
#endif
    }
}
