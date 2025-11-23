// Additional convenience extensions for SourceCache operations.
// Phase 1: expose predicate-based removals and batching helpers matching internal updater capabilities.

#pragma warning disable SA1503 // Braces should not be omitted

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Removes items whose keys match the specified predicate.
    /// </summary>
    public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TKey, bool> predicate)
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        source.Edit(updater => updater.RemoveKeys(predicate));
    }

    /// <summary>
    /// Removes items whose values match the specified predicate.
    /// </summary>
    public static void RemoveItems<TObject, TKey>(this ISourceCache<TObject, TKey> source, Func<TObject, bool> predicate)
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        source.Edit(updater => updater.RemoveItems(predicate));
    }
}
