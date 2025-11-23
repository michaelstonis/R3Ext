// Static predicate Filter operator for cache change sets.
#pragma warning disable SA1503
#pragma warning disable SA1513
#pragma warning disable SA1515
#pragma warning disable SA1116
#pragma warning disable SA1107

using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Filters a cache changeset by a predicate. Emits Add/Update/Remove/Refresh for items that satisfy the predicate.
    /// </summary>
    // Renamed to avoid ambiguity with R3.DynamicData.Operators.FilterOperator.Filter
    public static Observable<IChangeSet<TObject, TKey>> FilterCacheInternal<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, bool> predicate)
        where TKey : notnull
        where TObject : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));

        var state = new FilterCacheState<TObject, TKey>(source, predicate);
        return Observable.Create<IChangeSet<TObject, TKey>, FilterCacheState<TObject, TKey>>(
            state,
            static (observer, state) =>
            {
                var included = new HashSet<TKey>();

                return state.Source.Subscribe(changes =>
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
                                            outSet.Add(new Change<TObject, TKey>(ChangeReason.Add, change.Key, change.Current));
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
                }, observer.OnErrorResume, observer.OnCompleted);
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
