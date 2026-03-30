// Static predicate Filter operator for cache change sets.
// Audited against DD #1013 (bogus overload removal) and DD #1048 (Filter modernization).
// - DD #1013: No bogus no-predicate overload exists here; only FilterCacheInternal which requires a predicate. Not affected.
// - DD #1048: ChangeReason.Refresh is handled: re-evaluates predicate and emits Refresh (still passes),
//   Remove (stops passing), or Refresh (starts passing). Implementation is correct.
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for cache filtering operations.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Filters a cache changeset by a predicate. Emits Add/Update/Remove/Refresh for items that satisfy the predicate.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="predicate">The predicate to filter items.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    /// <remarks>Renamed to avoid ambiguity with R3.DynamicData.Operators.FilterOperator.Filter.</remarks>
    public static Observable<IChangeSet<TObject, TKey>> FilterCacheInternal<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, bool> predicate)
        where TKey : notnull
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var state = new FilterCacheState<TObject, TKey>(source, predicate);
        return Observable.Create<IChangeSet<TObject, TKey>, FilterCacheState<TObject, TKey>>(
            state,
            static (observer, state) =>
            {
                var included = new HashSet<TKey>();

                return state.Source.Subscribe(
                    changes =>
                {
                    try
                    {
                        var outSet = new ChangeSet<TObject, TKey>();

                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    {
                                        if (state.Predicate(change.Current))
                                        {
                                            included.Add(change.Key);
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Add, change.Key, change.Current));
                                        }

                                        break;
                                    }

                                case ChangeReason.Update:
                                    {
                                        var wasIncluded = included.Contains(change.Key);
                                        var nowIncluded = state.Predicate(change.Current);
                                        if (wasIncluded && nowIncluded)
                                        {
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Update, change.Key, change.Current, change.Previous.HasValue ? change.Previous.Value : change.Current));
                                        }
                                        else if (wasIncluded && !nowIncluded)
                                        {
                                            included.Remove(change.Key);
                                            var prev = change.Previous.HasValue ? change.Previous.Value : change.Current;
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, change.Key, prev, prev));
                                        }
                                        else if (!wasIncluded && nowIncluded)
                                        {
                                            included.Add(change.Key);
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Add, change.Key, change.Current));
                                        }

                                        // else both false: ignore
                                        break;
                                    }

                                case ChangeReason.Remove:
                                    {
                                        if (included.Remove(change.Key))
                                        {
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, change.Key, change.Current, change.Current));
                                        }

                                        break;
                                    }

                                case ChangeReason.Refresh:
                                    {
                                        var wasIncluded = included.Contains(change.Key);
                                        var nowIncluded = state.Predicate(change.Current);
                                        if (wasIncluded && nowIncluded)
                                        {
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Refresh, change.Key, change.Current));
                                        }
                                        else if (wasIncluded && !nowIncluded)
                                        {
                                            included.Remove(change.Key);
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, change.Key, change.Current, change.Current));
                                        }
                                        else if (!wasIncluded && nowIncluded)
                                        {
                                            included.Add(change.Key);
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Refresh, change.Key, change.Current, change.Current));
                                        }

                                        break;
                                    }

                                case ChangeReason.Moved:
                                    break;
                            }
                        }

                        if (outSet.Count > 0)
                        {
                            observer.OnNext(outSet);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                    observer.OnErrorResume,
                    observer.OnCompleted);
            });
    }

    private readonly struct FilterCacheState<TObj, TK>
        where TK : notnull
        where TObj : notnull
    {
        public readonly Observable<IChangeSet<TObj, TK>> Source;
        public readonly Func<TObj, bool> Predicate;

        public FilterCacheState(Observable<IChangeSet<TObj, TK>> source, Func<TObj, bool> predicate)
        {
            Source = source;
            Predicate = predicate;
        }
    }

    /// <summary>
    /// Filters a cache changeset using a predicate that combines item data with a changing state value.
    /// Avoids allocating a new predicate delegate on each state change (DD #941).
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TState">The type of the external filter state.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="stateStream">Observable that emits new state values. When a new state arrives, all cached items are re-evaluated.</param>
    /// <param name="predicate">Predicate that receives an item and the current state and returns true to include the item.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Filter<TObject, TKey, TState>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<TState> stateStream,
        Func<TObject, TState, bool> predicate)
        where TKey : notnull
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (stateStream is null)
        {
            throw new ArgumentNullException(nameof(stateStream));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var cache = new Dictionary<TKey, TObject>();
            var included = new HashSet<TKey>();
            TState currentState = default!;
            bool hasState = false;
            var disp = new CompositeDisposable();

            IChangeSet<TObject, TKey> EvaluateItem(TKey key, TObject item, ChangeReason incomingReason)
            {
                var outSet = new ChangeSet<TObject, TKey>();
                bool nowIncluded = hasState && predicate(item, currentState);
                bool wasIncluded = included.Contains(key);

                if (incomingReason == ChangeReason.Remove)
                {
                    if (wasIncluded)
                    {
                        included.Remove(key);
                        outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item, item));
                    }

                    cache.Remove(key);
                    return outSet;
                }

                // Add or Update
                cache[key] = item;
                if (wasIncluded && nowIncluded)
                {
                    if (incomingReason == ChangeReason.Update)
                    {
                        outSet.Add(new Change<TObject, TKey>(ChangeReason.Update, key, item));
                    }
                    else if (incomingReason == ChangeReason.Refresh)
                    {
                        outSet.Add(new Change<TObject, TKey>(ChangeReason.Refresh, key, item));
                    }
                }
                else if (wasIncluded && !nowIncluded)
                {
                    included.Remove(key);
                    outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, item, item));
                }
                else if (!wasIncluded && nowIncluded)
                {
                    included.Add(key);
                    outSet.Add(new Change<TObject, TKey>(ChangeReason.Add, key, item));
                }

                return outSet;
            }

            // Subscribe to state changes: re-evaluate all cached items
            stateStream.Subscribe(
                state =>
                {
                    try
                    {
                        currentState = state;
                        hasState = true;
                        var outSet = new ChangeSet<TObject, TKey>();
                        foreach (var kvp in cache)
                        {
                            bool nowIncluded = predicate(kvp.Value, currentState);
                            bool wasIncluded = included.Contains(kvp.Key);
                            if (!wasIncluded && nowIncluded)
                            {
                                included.Add(kvp.Key);
                                outSet.Add(new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                            }
                            else if (wasIncluded && !nowIncluded)
                            {
                                included.Remove(kvp.Key);
                                outSet.Add(new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value, kvp.Value));
                            }
                        }

                        if (outSet.Count > 0)
                        {
                            observer.OnNext(outSet);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disp);

            // Subscribe to source changes: apply current state
            source.Subscribe(
                changes =>
                {
                    try
                    {
                        var outSet = new ChangeSet<TObject, TKey>();
                        foreach (var change in changes)
                        {
                            var partial = EvaluateItem(change.Key, change.Current, change.Reason);
                            foreach (var c in partial)
                            {
                                outSet.Add(c);
                            }
                        }

                        if (outSet.Count > 0)
                        {
                            observer.OnNext(outSet);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disp);

            return disp;
        });
    }
}
