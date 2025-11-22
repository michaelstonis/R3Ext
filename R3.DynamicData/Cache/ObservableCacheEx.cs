// Port of DynamicData to R3.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using R3.DynamicData.Binding;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;

#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1116 // Parameters should begin on the line after the declaration when spanning multiple lines
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1514 // Element documentation header should be preceded by blank line
namespace R3.DynamicData.Cache;

/// <summary>
/// Observable cache extension methods.
/// </summary>
public static partial class ObservableCacheEx
{
    // Cache Transform moved to Operators.TransformOperator to avoid duplication.

    /// <summary>
    /// Extracts distinct values from the cache using a value selector, emitting change sets of unique values.
    /// </summary>
    public static Observable<IChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TValue> valueSelector,
        IEqualityComparer<TValue>? comparer = null)
        where TKey : notnull
        where TValue : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (valueSelector is null) throw new ArgumentNullException(nameof(valueSelector));
        var cmp = comparer ?? EqualityComparer<TValue>.Default;

        return Observable.Create<IChangeSet<TValue>>(observer =>
        {
            var refCounts = new Dictionary<TValue, int>(cmp);
            return source.Subscribe(changes =>
            {
                var changeSet = new ChangeSet<TValue>();
                foreach (var change in changes)
                {
                    var currentVal = valueSelector(change.Current);
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            if (!refCounts.TryGetValue(currentVal, out var count))
                            {
                                refCounts[currentVal] = 1;
                                changeSet.Add(new Change<TValue>(ListChangeReason.Add, currentVal, -1));
                            }
                            else
                            {
                                refCounts[currentVal] = count + 1;
                            }
                            break;
                        case ChangeReason.Update:
                            if (change.Previous.HasValue)
                            {
                                var prevVal = valueSelector(change.Previous.Value);
                                if (!cmp.Equals(prevVal, currentVal))
                                {
                                    // decrement previous
                                    if (refCounts.TryGetValue(prevVal, out var pc))
                                    {
                                        pc--;
                                        if (pc <= 0)
                                        {
                                            refCounts.Remove(prevVal);
                                            changeSet.Add(new Change<TValue>(ListChangeReason.Remove, prevVal, -1));
                                        }
                                        else
                                        {
                                            refCounts[prevVal] = pc;
                                        }
                                    }
                                    // increment new
                                    if (!refCounts.TryGetValue(currentVal, out var nc))
                                    {
                                        refCounts[currentVal] = 1;
                                        changeSet.Add(new Change<TValue>(ListChangeReason.Add, currentVal, -1));
                                    }
                                    else
                                    {
                                        refCounts[currentVal] = nc + 1;
                                    }
                                }
                            }
                            else
                            {
                                if (!refCounts.TryGetValue(currentVal, out var uc))
                                {
                                    refCounts[currentVal] = 1;
                                    changeSet.Add(new Change<TValue>(ListChangeReason.Add, currentVal, -1));
                                }
                                else
                                {
                                    refCounts[currentVal] = uc + 1;
                                }
                            }
                            break;
                        case ChangeReason.Remove:
                            var removeVal = change.Previous.HasValue ? valueSelector(change.Previous.Value) : currentVal;
                            if (refCounts.TryGetValue(removeVal, out var rc))
                            {
                                rc--;
                                if (rc <= 0)
                                {
                                    refCounts.Remove(removeVal);
                                    changeSet.Add(new Change<TValue>(ListChangeReason.Remove, removeVal, -1));
                                }
                                else
                                {
                                    refCounts[removeVal] = rc;
                                }
                            }
                            break;
                        case ChangeReason.Refresh:
                            // ignore for distinct values
                            break;
                        case ChangeReason.Moved:
                            break;
                    }
                }
                if (changeSet.Count > 0)
                {
                    observer.OnNext(changeSet);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    /// <summary>
    /// Flattens a collection returned for each cache item into a single list change set.
    /// If a comparer is provided, duplicates (as defined by the comparer) are suppressed globally and
    /// reference counted so that an item is only removed when the last source occurrence disappears.
    /// If no comparer is provided, all duplicates are emitted (previous behavior).
    /// </summary>
    public static Observable<IChangeSet<TDestination>> TransformMany<TObject, TKey, TDestination>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, IEnumerable<TDestination>> manySelector,
        IEqualityComparer<TDestination>? comparer = null)
        where TKey : notnull
        where TDestination : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (manySelector is null) throw new ArgumentNullException(nameof(manySelector));
        var cmp = comparer ?? EqualityComparer<TDestination>.Default;
        var useDedup = comparer != null; // Only dedup when user supplied a comparer.

        return Observable.Create<IChangeSet<TDestination>>(observer =>
        {
            // Only used when deduplication requested.
            var refCounts = useDedup ? new Dictionary<TDestination, int>(cmp) : null;

            return source.Subscribe(changes =>
            {
                var changeSet = new ChangeSet<TDestination>();
                foreach (var change in changes)
                {
                    IEnumerable<TDestination> curr = Enumerable.Empty<TDestination>();
                    IEnumerable<TDestination> prev = Enumerable.Empty<TDestination>();
                    if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update || change.Reason == ChangeReason.Refresh)
                    {
                        curr = manySelector(change.Current) ?? Enumerable.Empty<TDestination>();
                    }
                    if (change.Reason == ChangeReason.Remove || change.Reason == ChangeReason.Update)
                    {
                        if (change.Previous.HasValue)
                            prev = manySelector(change.Previous.Value) ?? Enumerable.Empty<TDestination>();
                    }

                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            if (!useDedup)
                            {
                                foreach (var item in curr)
                                {
                                    changeSet.Add(new Change<TDestination>(ListChangeReason.Add, item, -1));
                                }
                            }
                            else
                            {
                                foreach (var item in curr)
                                {
                                    if (!refCounts!.TryGetValue(item, out var count))
                                    {
                                        refCounts[item] = 1;
                                        changeSet.Add(new Change<TDestination>(ListChangeReason.Add, item, -1));
                                    }
                                    else
                                    {
                                        refCounts[item] = count + 1;
                                    }
                                }
                            }
                            break;

                        case ChangeReason.Update:
                            if (!useDedup)
                            {
                                foreach (var item in prev)
                                {
                                    changeSet.Add(new Change<TDestination>(ListChangeReason.Remove, item, -1));
                                }
                                foreach (var item in curr)
                                {
                                    changeSet.Add(new Change<TDestination>(ListChangeReason.Add, item, -1));
                                }
                            }
                            else
                            {
                                // Avoid remove+add thrash for items present in both prev and curr collections.
                                var prevSet = new HashSet<TDestination>(prev, cmp);
                                var currSet = new HashSet<TDestination>(curr, cmp);

                                // Items removed from this source entry
                                foreach (var removed in prevSet.Except(currSet))
                                {
                                    if (refCounts!.TryGetValue(removed, out var pc))
                                    {
                                        pc--;
                                        if (pc <= 0)
                                        {
                                            refCounts.Remove(removed);
                                            changeSet.Add(new Change<TDestination>(ListChangeReason.Remove, removed, -1));
                                        }
                                        else
                                        {
                                            refCounts[removed] = pc;
                                        }
                                    }
                                }

                                // Items newly added in this source entry
                                foreach (var added in currSet.Except(prevSet))
                                {
                                    if (!refCounts!.TryGetValue(added, out var nc))
                                    {
                                        refCounts[added] = 1;
                                        changeSet.Add(new Change<TDestination>(ListChangeReason.Add, added, -1));
                                    }
                                    else
                                    {
                                        refCounts[added] = nc + 1;
                                    }
                                }
                            }
                            break;

                        case ChangeReason.Remove:
                            if (!useDedup)
                            {
                                foreach (var item in prev)
                                {
                                    changeSet.Add(new Change<TDestination>(ListChangeReason.Remove, item, -1));
                                }
                            }
                            else
                            {
                                foreach (var item in prev)
                                {
                                    if (refCounts!.TryGetValue(item, out var rc))
                                    {
                                        rc--;
                                        if (rc <= 0)
                                        {
                                            refCounts.Remove(item);
                                            changeSet.Add(new Change<TDestination>(ListChangeReason.Remove, item, -1));
                                        }
                                        else
                                        {
                                            refCounts[item] = rc;
                                        }
                                    }
                                }
                            }
                            break;

                        case ChangeReason.Refresh:
                            // No structural changes.
                            break;
                        case ChangeReason.Moved:
                            // Position not tracked for flattened list (-1 indices); ignore.
                            break;
                    }
                }
                if (changeSet.Count > 0)
                {
                    observer.OnNext(changeSet);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // Dynamic predicate Filter provided by Operators.FilterOperator; removed duplicate implementation.

    /// <summary>
    /// Binds a cache changeset to an IList, applying Add/Update/Remove operations.
    /// Note: SourceCache is inherently unordered, so items will be added/updated/removed
    /// without regard to position. Consider using Sort + Bind for ordered scenarios.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="target">The target list to bind to.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IList<TObject> target)
        where TKey : notnull
    {
        return source.Subscribe(changeSet =>
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        target.Add(change.Current);
                        break;

                    case ChangeReason.Update:
                        // Update: remove old value and add new value
                        // Since cache is unordered, we remove by finding the previous item
                        if (change.Previous.HasValue)
                        {
                            target.Remove(change.Previous.Value);
                        }

                        target.Add(change.Current);
                        break;

                    case ChangeReason.Remove:
                        target.Remove(change.Current);
                        break;

                    case ChangeReason.Refresh:
                        // Refresh doesn't require action for basic binding
                        break;

                    case ChangeReason.Moved:
                        // Moved is not applicable to unordered cache binding
                        break;
                }
            }
        });
    }

    /// <summary>
    /// Binds a cache changeset to an IObservableCollection, with reset threshold support.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="targetCollection">The target observable collection to bind to.</param>
    /// <param name="resetThreshold">The threshold for resetting the collection instead of applying individual changes.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IObservableCollection<TObject> targetCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TKey : notnull
    {
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionCacheAdaptor<TObject, TKey>(targetCollection, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }

    /// <summary>
    /// Binds a cache changeset to a ReadOnlyObservableCollection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset.</param>
    /// <param name="readOnlyObservableCollection">The resulting read-only observable collection.</param>
    /// <param name="resetThreshold">The threshold for resetting the collection instead of applying individual changes.</param>
    /// <returns>A disposable to stop the binding.</returns>
    public static IDisposable Bind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TKey : notnull
    {
        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionCacheAdaptor<TObject, TKey>(target, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }

    // Lifecycle parity operators

    public static Observable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, IDisposable> subscriptionFactory)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (subscriptionFactory is null) throw new ArgumentNullException(nameof(subscriptionFactory));
        return new Cache.Internal.SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
    }

    public static Observable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new Cache.Internal.DisposeMany<TObject, TKey>(source).Run();
    }

    public static Observable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Action<TObject> disposeAction)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (disposeAction is null) throw new ArgumentNullException(nameof(disposeAction));
        return new Cache.Internal.DisposeMany<TObject, TKey>(source, disposeAction).Run();
    }

    // AutoRefresh variants for cache

    public static Observable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        TimeSpan? changeSetBuffer = null,
        TimeSpan? propertyChangeThrottle = null,
        TimeProvider? timeProvider = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                    return t.WhenAnyPropertyChanged();
                return t.WhenAnyPropertyChanged().Debounce(propertyChangeThrottle.Value, timeProvider ?? ObservableSystem.DefaultTimeProvider);
            },
            changeSetBuffer,
            timeProvider);
    }

    public static Observable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey, TProperty>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Expression<Func<TObject, TProperty>> propertyAccessor,
        TimeSpan? changeSetBuffer = null,
        TimeSpan? propertyChangeThrottle = null,
        TimeProvider? timeProvider = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (propertyAccessor is null) throw new ArgumentNullException(nameof(propertyAccessor));
        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                    return t.WhenPropertyChanged(propertyAccessor, false);
                return t.WhenPropertyChanged(propertyAccessor, false).Debounce(propertyChangeThrottle.Value, timeProvider ?? ObservableSystem.DefaultTimeProvider);
            },
            changeSetBuffer,
            timeProvider);
    }

    public static Observable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TAny>> reevaluator,
        TimeSpan? changeSetBuffer = null,
        TimeProvider? timeProvider = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (reevaluator is null) throw new ArgumentNullException(nameof(reevaluator));

        // Adapt underlying generic AutoRefresh implementation which operates on object keys.
        var castSource = source.Select(cs =>
        {
            var converted = new ChangeSet<TObject, object>(cs.Count);
            foreach (var ch in cs)
            {
                converted.Add(ch.Previous.HasValue
                    ? new Change<TObject, object>(ch.Reason, ch.Key!, ch.Current, ch.Previous.Value)
                    : new Change<TObject, object>(ch.Reason, ch.Key!, ch.Current));
            }
            return (IChangeSet<TObject, object>)converted;
        });
        return new Cache.Internal.AutoRefresh<TObject, TAny>(castSource, reevaluator, changeSetBuffer, timeProvider)
            .Run()
            .Select(cs =>
            {
                var converted = new ChangeSet<TObject, TKey>(cs.Count);
                foreach (var ch in cs)
                {
                    converted.Add(ch.Previous.HasValue
                        ? new Change<TObject, TKey>(ch.Reason, (TKey)ch.Key!, ch.Current, ch.Previous.Value)
                        : new Change<TObject, TKey>(ch.Reason, (TKey)ch.Key!, ch.Current));
                }
                return (IChangeSet<TObject, TKey>)converted;
            });
    }

    // Filtering parity
    public static Observable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<bool>> observablePredicate)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (observablePredicate is null) throw new ArgumentNullException(nameof(observablePredicate));
        return new Cache.Internal.FilterOnObservable<TObject, TKey, bool>(source, observablePredicate).Run();
    }

    // Expiration
    public static Observable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TimeSpan?> expireSelector,
        TimeProvider? timeProvider = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (expireSelector is null) throw new ArgumentNullException(nameof(expireSelector));
        return new Cache.Internal.ExpireAfter<TObject, TKey>(source, expireSelector, timeProvider).Run();
    }

    // EnsureUniqueKeys
    public static Observable<IChangeSet<TObject, TKey>> EnsureUniqueKeys<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new Cache.Internal.EnsureUniqueKeys<TObject, TKey>(source).Run();
    }

    // SuppressRefresh
    public static Observable<IChangeSet<TObject, TKey>> SuppressRefresh<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new Cache.Internal.SuppressRefresh<TObject, TKey>(source).Run();
    }

    // IncludeUpdateWhen
    public static Observable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TObject?, bool> predicate)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return new Cache.Internal.IncludeUpdateWhen<TObject, TKey>(source, predicate).Run();
    }

    // WatchValue convenience (single key stream)
    public static Observable<Change<TObject, TKey>> WatchValue<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Observable.Create<Change<TObject, TKey>>(observer =>
        {
            return source.Subscribe(changes =>
            {
                foreach (var c in changes)
                {
                    if (EqualityComparer<TKey>.Default.Equals(c.Key, key))
                        observer.OnNext(c);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }
}
