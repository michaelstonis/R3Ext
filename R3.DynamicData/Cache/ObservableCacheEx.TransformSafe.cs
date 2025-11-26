// Port of DynamicData TransformSafe to R3.
using System;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for observable cache change sets.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
    /// <returns>A transformed observable.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> TransformSafe<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> transformFactory,
        Action<Error<TSource, TKey>> errorHandler)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (transformFactory is null)
        {
            throw new ArgumentNullException(nameof(transformFactory));
        }

        if (errorHandler is null)
        {
            throw new ArgumentNullException(nameof(errorHandler));
        }

        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var cache = new Dictionary<TKey, TDestination>();

            return source.Subscribe(
                changeSet =>
            {
                var transformedSet = new ChangeSet<TDestination, TKey>();

                foreach (var change in changeSet)
                {
                    try
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                var transformed = transformFactory(change.Current);
                                var hadPrevious = cache.TryGetValue(change.Key, out var prev);
                                cache[change.Key] = transformed;

                                var reason = hadPrevious ? ChangeReason.Update : ChangeReason.Add;
                                if (hadPrevious)
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(reason, change.Key, transformed, prev!));
                                }
                                else
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(reason, change.Key, transformed));
                                }

                                break;

                            case ChangeReason.Remove:
                                if (cache.TryGetValue(change.Key, out var removed))
                                {
                                    cache.Remove(change.Key);
                                    transformedSet.Add(new Change<TDestination, TKey>(ChangeReason.Remove, change.Key, removed, removed));
                                }

                                break;

                            case ChangeReason.Refresh:
                                if (cache.ContainsKey(change.Key))
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(ChangeReason.Refresh, change.Key, cache[change.Key]));
                                }

                                break;

                            case ChangeReason.Moved:

                                // Cache moves not applicable; ignore.
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Call error handler instead of killing the stream
                        errorHandler(new Error<TSource, TKey>(ex, change.Current, change.Key));
                    }
                }

                if (transformedSet.Count > 0)
                {
                    observer.OnNext(transformedSet);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
    /// <returns>A transformed observable.</returns>
    public static Observable<IChangeSet<TDestination, TKey>> TransformSafe<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TKey, TDestination> transformFactory,
        Action<Error<TSource, TKey>> errorHandler)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (transformFactory is null)
        {
            throw new ArgumentNullException(nameof(transformFactory));
        }

        if (errorHandler is null)
        {
            throw new ArgumentNullException(nameof(errorHandler));
        }

        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var cache = new Dictionary<TKey, TDestination>();

            return source.Subscribe(
                changeSet =>
            {
                var transformedSet = new ChangeSet<TDestination, TKey>();

                foreach (var change in changeSet)
                {
                    try
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                var transformed = transformFactory(change.Current, change.Key);
                                var hadPrevious = cache.TryGetValue(change.Key, out var prev);
                                cache[change.Key] = transformed;

                                var reason = hadPrevious ? ChangeReason.Update : ChangeReason.Add;
                                if (hadPrevious)
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(reason, change.Key, transformed, prev!));
                                }
                                else
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(reason, change.Key, transformed));
                                }

                                break;

                            case ChangeReason.Remove:
                                if (cache.TryGetValue(change.Key, out var removed))
                                {
                                    cache.Remove(change.Key);
                                    transformedSet.Add(new Change<TDestination, TKey>(ChangeReason.Remove, change.Key, removed, removed));
                                }

                                break;

                            case ChangeReason.Refresh:
                                if (cache.ContainsKey(change.Key))
                                {
                                    transformedSet.Add(new Change<TDestination, TKey>(ChangeReason.Refresh, change.Key, cache[change.Key]));
                                }

                                break;

                            case ChangeReason.Moved:

                                // Cache moves not applicable; ignore.
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Call error handler instead of killing the stream
                        errorHandler(new Error<TSource, TKey>(ex, change.Current, change.Key));
                    }
                }

                if (transformedSet.Count > 0)
                {
                    observer.OnNext(transformedSet);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }
}
