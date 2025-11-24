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
        Log($"[BindingRegistry] TryCreateOneWay lookup {key} for {typeof(TFrom).Name}->{typeof(TTarget).Name}");
        if (_oneWay.TryGetValue(key, out List<OneWayEntry>? list))
        {
            // AOT-compatible: Use exact type match based on generic parameters, not runtime types
            OneWayEntry? entry = PickBestExact<TFrom, TTarget>(list);
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
        Log($"[BindingRegistry] TryCreateTwoWay lookup {key} for {typeof(TFrom).Name}<->{typeof(TTarget).Name}");
        if (_twoWay.TryGetValue(key, out List<TwoWayEntry>? list))
        {
            // AOT-compatible: Use exact type match based on generic parameters, not runtime types
            TwoWayEntry? entry = PickBestExact<TFrom, TTarget>(list);
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
        Log($"[BindingRegistry] TryCreateWhenChanged lookup {typePart}|{pathPart} for {typeof(TObj).Name}");
        if (_whenChanged.TryGetValue(pathPart, out List<WhenEntry>? list))
        {
            // AOT-compatible: Use exact type match based on generic parameter, not runtime type
            WhenEntry? entry = PickBestExact<TObj>(list);
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

    // AOT-compatible: Exact type matching using generics instead of runtime type inspection
    private static OneWayEntry? PickBestExact<TFrom, TTarget>(List<OneWayEntry> list)
    {
        Type fromType = typeof(TFrom);
        Type targetType = typeof(TTarget);

        // First, try exact match
        foreach (OneWayEntry e in list)
        {
            if (e.FromType == fromType && e.TargetType == targetType)
            {
                return e;
            }
        }

        // If no exact match, look for compatible base types (still AOT-safe with typeof)
        // This preserves some polymorphism while avoiding runtime GetType()
        foreach (OneWayEntry e in list)
        {
            if (e.FromType.IsAssignableFrom(fromType) && e.TargetType.IsAssignableFrom(targetType))
            {
                return e;
            }
        }

        return null;
    }

    private static TwoWayEntry? PickBestExact<TFrom, TTarget>(List<TwoWayEntry> list)
    {
        Type fromType = typeof(TFrom);
        Type targetType = typeof(TTarget);

        // First, try exact match
        foreach (TwoWayEntry e in list)
        {
            if (e.FromType == fromType && e.TargetType == targetType)
            {
                return e;
            }
        }

        // If no exact match, look for compatible base types (still AOT-safe with typeof)
        foreach (TwoWayEntry e in list)
        {
            if (e.FromType.IsAssignableFrom(fromType) && e.TargetType.IsAssignableFrom(targetType))
            {
                return e;
            }
        }

        return null;
    }

    private static WhenEntry? PickBestExact<TObj>(List<WhenEntry> list)
    {
        Type objType = typeof(TObj);

        // First, try exact match
        foreach (WhenEntry e in list)
        {
            if (e.ObjType == objType)
            {
                return e;
            }
        }

        // If no exact match, look for compatible base types (still AOT-safe with typeof)
        foreach (WhenEntry e in list)
        {
            if (e.ObjType.IsAssignableFrom(objType))
            {
                return e;
            }
        }

        return null;
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
