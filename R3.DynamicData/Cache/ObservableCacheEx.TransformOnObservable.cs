// Port of DynamicData to R3.
namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for observable cache change sets.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Transforms each item in the cache by subscribing to a per-item observable.
    /// Emits update changesets whenever the per-item observable produces a new value.
    /// Preserves changeset ordering.
    /// </summary>
    /// <typeparam name="TObject">The type of the source objects in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the transformed objects.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="observableSelector">Function that, given a source item and its key, returns an observable of destination values.</param>
    /// <returns>An observable that emits change sets of transformed destination items.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> TransformOnObservable<TObject, TKey, TDestination>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, Observable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector is null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        return new Internal.TransformOnObservable<TObject, TKey, TDestination>(source, observableSelector).Run();
    }
}
