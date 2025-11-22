// Public extension for ChangeKey operator.
#pragma warning disable SA1516, SA1515, SA1503
using System;
using R3.DynamicData.Cache.Internal;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Projects the cache into a new key space using the provided selector.
    /// Emits change sets keyed by <typeparamref name="TNewKey"/> reflecting key changes as Remove+Add pairs.
    /// </summary>
    public static Observable<IChangeSet<TObject, TNewKey>> ChangeKey<TObject, TOldKey, TNewKey>(
        this Observable<IChangeSet<TObject, TOldKey>> source,
        Func<TObject, TNewKey> keySelector)
        where TObject : notnull
        where TOldKey : notnull
        where TNewKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        return new ChangeKeyOperator<TObject, TOldKey, TNewKey>(source, keySelector).Run();
    }
}
