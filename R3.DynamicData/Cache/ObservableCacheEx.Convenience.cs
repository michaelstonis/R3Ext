// Additional convenience extensions for SourceCache operations.
// Phase 1: expose predicate-based removals and batching helpers matching internal updater capabilities.

namespace R3.DynamicData.Cache;

/// <summary>Extension methods for convenient cache operations.</summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Removes items whose keys match the specified predicate.
    /// </summary>
    /// <typeparam name="TObject">The type of objects in the cache.</typeparam>
    /// <typeparam name="TKey">The type of keys in the cache.</typeparam>
    /// <param name="source">The source cache.</param>
    /// <param name="predicate">A predicate to determine which keys to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when source or predicate is null.</exception>
    public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TKey, bool> predicate)
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        source.Edit(updater => updater.RemoveKeys(predicate));
    }

    /// <summary>
    /// Removes items whose values match the specified predicate.
    /// </summary>
    /// <typeparam name="TObject">The type of objects in the cache.</typeparam>
    /// <typeparam name="TKey">The type of keys in the cache.</typeparam>
    /// <param name="source">The source cache.</param>
    /// <param name="predicate">A predicate to determine which items to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when source or predicate is null.</exception>
    public static void RemoveItems<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TObject, bool> predicate)
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        source.Edit(updater => updater.RemoveItems(predicate));
    }
}
