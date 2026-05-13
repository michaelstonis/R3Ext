using System.Collections.Concurrent;
using System.ComponentModel;

namespace R3Ext.Utilities;

/// <summary>
/// Cache for PropertyChangedEventArgs and PropertyChangingEventArgs to avoid repeated allocations.
/// Uses a thread-safe concurrent dictionary with a size limit to prevent unbounded growth.
/// </summary>
internal static class PropertyEventArgsCache
{
    private const int MaxCacheSize = 256;

    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> ChangedCache = new();
    private static readonly ConcurrentDictionary<string, PropertyChangingEventArgs> ChangingCache = new();

    /// <summary>
    /// Gets or creates a cached PropertyChangedEventArgs for the specified property name.
    /// </summary>
    public static PropertyChangedEventArgs GetPropertyChanged(string propertyName)
    {
        // Only cache if we haven't exceeded the limit (approximate check for performance)
        if (ChangedCache.Count < MaxCacheSize)
        {
            // Atomically get or add to cache - avoids allocation on cache hit
            return ChangedCache.GetOrAdd(propertyName, static n => new PropertyChangedEventArgs(n));
        }

        // If cache is full, check if already cached, otherwise allocate new
        return ChangedCache.TryGetValue(propertyName, out var cached)
            ? cached
            : new PropertyChangedEventArgs(propertyName);
    }

    /// <summary>
    /// Gets or creates a cached PropertyChangingEventArgs for the specified property name.
    /// </summary>
    public static PropertyChangingEventArgs GetPropertyChanging(string propertyName)
    {
        // Only cache if we haven't exceeded the limit (approximate check for performance)
        if (ChangingCache.Count < MaxCacheSize)
        {
            // Atomically get or add to cache - avoids allocation on cache hit
            return ChangingCache.GetOrAdd(propertyName, static n => new PropertyChangingEventArgs(n));
        }

        // If cache is full, check if already cached, otherwise allocate new
        return ChangingCache.TryGetValue(propertyName, out var cached)
            ? cached
            : new PropertyChangingEventArgs(propertyName);
    }
}
