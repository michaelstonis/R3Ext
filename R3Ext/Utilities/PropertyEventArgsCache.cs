using System.Collections.Concurrent;
using System.ComponentModel;

namespace R3Ext.Utilities;

/// <summary>
/// Cache for PropertyChangedEventArgs and PropertyChangingEventArgs to avoid repeated allocations.
/// Uses a thread-safe concurrent dictionary with a size limit to prevent unbounded growth.
/// </summary>
internal static class PropertyEventArgsCache
{
    private const int MaxCacheSize = 128;

    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> ChangedCache = new();
    private static readonly ConcurrentDictionary<string, PropertyChangingEventArgs> ChangingCache = new();

    /// <summary>
    /// Gets or creates a cached PropertyChangedEventArgs for the specified property name.
    /// </summary>
    public static PropertyChangedEventArgs GetPropertyChanged(string propertyName)
    {
        // Try to get from cache first
        if (ChangedCache.TryGetValue(propertyName, out var cached))
        {
            return cached;
        }

        // Create new and try to add to cache
        var args = new PropertyChangedEventArgs(propertyName);

        // Only cache if we haven't exceeded the limit (simple check, may slightly exceed)
        if (ChangedCache.Count < MaxCacheSize)
        {
            ChangedCache.TryAdd(propertyName, args);
        }

        return args;
    }

    /// <summary>
    /// Gets or creates a cached PropertyChangingEventArgs for the specified property name.
    /// </summary>
    public static PropertyChangingEventArgs GetPropertyChanging(string propertyName)
    {
        // Try to get from cache first
        if (ChangingCache.TryGetValue(propertyName, out var cached))
        {
            return cached;
        }

        // Create new and try to add to cache
        var args = new PropertyChangingEventArgs(propertyName);

        // Only cache if we haven't exceeded the limit (simple check, may slightly exceed)
        if (ChangingCache.Count < MaxCacheSize)
        {
            ChangingCache.TryAdd(propertyName, args);
        }

        return args;
    }
}
