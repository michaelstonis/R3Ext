// Port of DynamicData TransformAsync to R3.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace R3.DynamicData.Cache;

/// <summary>Async transformation extension methods for observable caches.</summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Asynchronously transforms items in the cache using a task-based selector.
    /// When an item is removed before its transformation completes, the task is cancelled.
    /// </summary>
    /// <typeparam name="TSource">The type of source objects.</typeparam>
    /// <typeparam name="TKey">The type of keys.</typeparam>
    /// <typeparam name="TDestination">The type of destination objects.</typeparam>
    /// <param name="source">The source observable cache change set.</param>
    /// <param name="transformFactory">The async transformation function.</param>
    /// <returns>An observable of change sets with transformed items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or transformFactory is null.</exception>
    public static Observable<IChangeSet<TDestination, TKey>> TransformAsync<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, Task<TDestination>> transformFactory)
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

        return TransformAsync(source, (item, _) => transformFactory(item));
    }

    /// <summary>
    /// Asynchronously transforms items in the cache using a task-based selector with cancellation support.
    /// When an item is removed before its transformation completes, the cancellation token is triggered.
    /// </summary>
    /// <typeparam name="TSource">The type of source objects.</typeparam>
    /// <typeparam name="TKey">The type of keys.</typeparam>
    /// <typeparam name="TDestination">The type of destination objects.</typeparam>
    /// <param name="source">The source observable cache change set.</param>
    /// <param name="transformFactory">The async transformation function with cancellation token.</param>
    /// <returns>An observable of change sets with transformed items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or transformFactory is null.</exception>
    public static Observable<IChangeSet<TDestination, TKey>> TransformAsync<TSource, TKey, TDestination>(
        this Observable<IChangeSet<TSource, TKey>> source,
        Func<TSource, CancellationToken, Task<TDestination>> transformFactory)
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

        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            var transformations = new Dictionary<TKey, PendingTransformation>();
            var completed = new Dictionary<TKey, TDestination>();
            var gate = new object();

            return source.Subscribe(
                changeSet =>
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case Kernel.ChangeReason.Add:
                        case Kernel.ChangeReason.Update:
                            HandleAddOrUpdate(change, transformFactory, transformations, completed, observer, gate);
                            break;

                        case Kernel.ChangeReason.Remove:
                            HandleRemove(change.Key, transformations, completed, observer, gate);
                            break;

                        case Kernel.ChangeReason.Refresh:
                            // Refresh does not change value; re-emit if already completed.
                            if (completed.ContainsKey(change.Key))
                            {
                                var refreshSet = new ChangeSet<TDestination, TKey>();
                                refreshSet.Add(new Change<TDestination, TKey>(Kernel.ChangeReason.Refresh, change.Key, completed[change.Key]));
                                observer.OnNext(refreshSet);
                            }

                            break;

                        case Kernel.ChangeReason.Moved:
                            // Cache moves not applicable; ignore.
                            break;
                    }
                }
            },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    private static void HandleAddOrUpdate<TSource, TKey, TDestination>(
        Change<TSource, TKey> change,
        Func<TSource, CancellationToken, Task<TDestination>> transformFactory,
        Dictionary<TKey, PendingTransformation> transformations,
        Dictionary<TKey, TDestination> completed,
        Observer<IChangeSet<TDestination, TKey>> observer,
        object gate)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        var key = change.Key;
        var item = change.Current;
        var isUpdate = change.Reason == Kernel.ChangeReason.Update;

        // Cancel existing transformation if present.
        if (transformations.TryGetValue(key, out var existing))
        {
            existing.Cts.Cancel();
            existing.Cts.Dispose();
            transformations.Remove(key);
        }

        var cts = new CancellationTokenSource();
        transformations[key] = new PendingTransformation(cts);

        Task.Run(
            async () =>
        {
            try
            {
                var result = await transformFactory(item, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    lock (gate)
                    {
                        if (transformations.TryGetValue(key, out var pending) && pending.Cts == cts)
                        {
                            transformations.Remove(key);
                            cts.Dispose();

                            var hadPrevious = completed.TryGetValue(key, out var prev);
                            completed[key] = result;

                            var reason = hadPrevious && isUpdate
                                ? Kernel.ChangeReason.Update
                                : Kernel.ChangeReason.Add;

                            var resultSet = new ChangeSet<TDestination, TKey>();
                            if (hadPrevious && reason == Kernel.ChangeReason.Update)
                            {
                                resultSet.Add(new Change<TDestination, TKey>(reason, key, result, prev!));
                            }
                            else
                            {
                                resultSet.Add(new Change<TDestination, TKey>(reason, key, result));
                            }

                            observer.OnNext(resultSet);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled; ignore.
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    observer.OnErrorResume(ex);
                }
            }
        },
            cts.Token);
    }

    private static void HandleRemove<TKey, TDestination>(
        TKey key,
        Dictionary<TKey, PendingTransformation> transformations,
        Dictionary<TKey, TDestination> completed,
        Observer<IChangeSet<TDestination, TKey>> observer,
        object gate)
        where TKey : notnull
        where TDestination : notnull
    {
        lock (gate)
        {
            // Cancel pending transformation if present.
            if (transformations.TryGetValue(key, out var pending))
            {
                pending.Cts.Cancel();
                pending.Cts.Dispose();
                transformations.Remove(key);
            }

            // Emit remove if already completed.
            if (completed.TryGetValue(key, out var dest))
            {
                completed.Remove(key);
                var removeSet = new ChangeSet<TDestination, TKey>();
                removeSet.Add(new Change<TDestination, TKey>(Kernel.ChangeReason.Remove, key, dest, dest));
                observer.OnNext(removeSet);
            }
        }
    }

    private sealed class PendingTransformation
    {
        public CancellationTokenSource Cts { get; }

        public PendingTransformation(CancellationTokenSource cts)
        {
            Cts = cts;
        }
    }
}
