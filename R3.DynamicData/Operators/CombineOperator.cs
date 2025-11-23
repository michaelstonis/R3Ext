// Port of DynamicData to R3.

using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Operators;

/// <summary>
/// Extension methods for combining multiple observable cache change sets.
/// </summary>
public static class CombineOperator
{
    /// <summary>
    /// Combines multiple observable cache change sets into a single change set.
    /// When multiple sources have the same key, the last source in the list wins.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The source observables to combine.</param>
    /// <returns>An observable that emits the combined change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(
        params Observable<IChangeSet<TObject, TKey>>[] sources)
        where TKey : notnull
    {
        if (sources == null || sources.Length == 0)
        {
            throw new ArgumentException("At least one source is required", nameof(sources));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            // Track the current state from each source
            var sourceCaches = new Dictionary<TKey, (TObject Value, int SourceIndex)>();
            var subscriptions = new List<IDisposable>();

            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var index = sourceIndex; // Capture for closure
                var subscription = sources[index].Subscribe(
                    changes =>
                    {
                        try
                        {
                            var outputChanges = new ChangeSet<TObject, TKey>();

                            foreach (var change in changes)
                            {
                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        if (sourceCaches.TryGetValue(change.Key, out var existing))
                                        {
                                            // Update if this source has higher priority (later in the list)
                                            // or if it's from the same source
                                            if (index >= existing.SourceIndex)
                                            {
                                                sourceCaches[change.Key] = (change.Current, index);

                                                var reason = existing.SourceIndex == index && change.Reason == ChangeReason.Update
                                                    ? ChangeReason.Update
                                                    : ChangeReason.Update;

                                                outputChanges.Add(new Change<TObject, TKey>(
                                                    reason,
                                                    change.Key,
                                                    change.Current,
                                                    existing.Value));
                                            }
                                        }
                                        else
                                        {
                                            // New key
                                            sourceCaches[change.Key] = (change.Current, index);
                                            outputChanges.Add(new Change<TObject, TKey>(
                                                ChangeReason.Add,
                                                change.Key,
                                                change.Current));
                                        }

                                        break;

                                    case ChangeReason.Remove:
                                        if (sourceCaches.TryGetValue(change.Key, out var removed))
                                        {
                                            if (removed.SourceIndex == index)
                                            {
                                                // This source owned the key, so remove it
                                                sourceCaches.Remove(change.Key);
                                                outputChanges.Add(new Change<TObject, TKey>(
                                                    ChangeReason.Remove,
                                                    change.Key,
                                                    removed.Value,
                                                    removed.Value));
                                            }
                                        }

                                        break;

                                    case ChangeReason.Refresh:
                                        if (sourceCaches.TryGetValue(change.Key, out var refreshed))
                                        {
                                            if (refreshed.SourceIndex == index)
                                            {
                                                outputChanges.Add(new Change<TObject, TKey>(
                                                    ChangeReason.Refresh,
                                                    change.Key,
                                                    change.Current));
                                            }
                                        }

                                        break;
                                }
                            }

                            if (outputChanges.Count > 0)
                            {
                                observer.OnNext(outputChanges);
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnErrorResume(ex);
                        }
                    },
                    observer.OnErrorResume,
                    observer.OnCompleted);

                subscriptions.Add(subscription);
            }

            return Disposable.Create(() =>
            {
                foreach (var sub in subscriptions)
                {
                    sub.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Combines multiple observable cache change sets from an enumerable into a single change set.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The source observables to combine.</param>
    /// <returns>An observable that emits the combined change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(
        this IEnumerable<Observable<IChangeSet<TObject, TKey>>> sources)
        where TKey : notnull
    {
        return Combine(sources.ToArray());
    }
}
