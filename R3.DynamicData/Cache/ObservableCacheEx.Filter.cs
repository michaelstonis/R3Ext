// Static predicate Filter operator for cache change sets.
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
}
