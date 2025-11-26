// Public extension for ChangeKey operator.
using System;
using R3.DynamicData.Cache.Internal;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for cache change key operations.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Projects the cache into a new key space using the provided selector.
    /// Emits change sets keyed by <typeparamref name="TNewKey"/> reflecting key changes as Remove+Add pairs.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the cache.</typeparam>
    /// <typeparam name="TOldKey">The original key type.</typeparam>
    /// <typeparam name="TNewKey">The new key type.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="keySelector">A function to select the new key from each object.</param>
    /// <returns>An observable that emits change sets with the new key type.</returns>
    public static Observable<IChangeSet<TObject, TNewKey>> ChangeKey<TObject, TOldKey, TNewKey>(
        this Observable<IChangeSet<TObject, TOldKey>> source,
        Func<TObject, TNewKey> keySelector)
        where TObject : notnull
        where TOldKey : notnull
        where TNewKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        return new ChangeKeyOperator<TObject, TOldKey, TNewKey>(source, keySelector).Run();
    }
}
