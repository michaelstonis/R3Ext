
// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Operators;

/// <summary>
/// Extension methods for transforming observable change sets.
/// </summary>
public static class TransformOperator
{
    /// <summary>
    /// Transforms each item in the change set using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="transformFactory">The transform function.</param>
    /// <returns>An observable that emits transformed change sets.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> Transform<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> transformFactory)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (transformFactory == null)
        {
            throw new ArgumentNullException(nameof(transformFactory));
        }

        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            return source.Subscribe(
                changes =>
                {
                    var transformed = new ChangeSet<TDestination, TKey>(changes.Count);

                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Add,
                                    change.Key,
                                    transformFactory(change.Current)));
                                break;

                            case Kernel.ChangeReason.Update:
                                var transformedCurrent = transformFactory(change.Current);
                                var transformedPrevious = change.Previous.HasValue
                                    ? transformFactory(change.Previous.Value)
                                    : default(TDestination)!;

                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Update,
                                    change.Key,
                                    transformedCurrent,
                                    transformedPrevious));
                                break;

                            case Kernel.ChangeReason.Remove:
                                var removedItem = transformFactory(change.Current);
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Remove,
                                    change.Key,
                                    removedItem,
                                    removedItem));
                                break;

                            case Kernel.ChangeReason.Refresh:
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Refresh,
                                    change.Key,
                                    transformFactory(change.Current)));
                                break;
                        }
                    }

                    observer.OnNext(transformed);
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Transforms each item in the change set using the specified transform function with key.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="transformFactory">The transform function that receives both the object and its key.</param>
    /// <returns>An observable that emits transformed change sets.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> Transform<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TKey, TDestination> transformFactory)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (transformFactory == null)
        {
            throw new ArgumentNullException(nameof(transformFactory));
        }

        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            return source.Subscribe(
                changes =>
                {
                    var transformed = new ChangeSet<TDestination, TKey>(changes.Count);

                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Add,
                                    change.Key,
                                    transformFactory(change.Current, change.Key)));
                                break;

                            case Kernel.ChangeReason.Update:
                                var transformedCurrent = transformFactory(change.Current, change.Key);
                                var transformedPrevious = change.Previous.HasValue
                                    ? transformFactory(change.Previous.Value, change.Key)
                                    : default(TDestination)!;

                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Update,
                                    change.Key,
                                    transformedCurrent,
                                    transformedPrevious));
                                break;

                            case Kernel.ChangeReason.Remove:
                                var removedItem = transformFactory(change.Current, change.Key);
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Remove,
                                    change.Key,
                                    removedItem,
                                    removedItem));
                                break;

                            case Kernel.ChangeReason.Refresh:
                                transformed.Add(new Change<TDestination, TKey>(
                                    Kernel.ChangeReason.Refresh,
                                    change.Key,
                                    transformFactory(change.Current, change.Key)));
                                break;
                        }
                    }

                    observer.OnNext(transformed);
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }
}
