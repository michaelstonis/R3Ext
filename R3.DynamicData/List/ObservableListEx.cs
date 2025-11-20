// Port of DynamicData to R3.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using R3.DynamicData.Binding;
using R3.DynamicData.List.Internal;

namespace R3.DynamicData.List;

public static class ObservableListEx
{
    public static Observable<IChangeSet<TResult>> Transform<T, TResult>(
        this Observable<IChangeSet<T>> source,
        Func<T, TResult> selector)
    {
        return new Internal.Transform<T, TResult>(source, selector).Run();
    }

    public static Observable<IChangeSet<T>> Sort<T>(
        this Observable<IChangeSet<T>> source,
        IComparer<T> comparer,
        SortOptions options = SortOptions.None)
    {
        return new Sort<T>(source, comparer, options).Run();
    }

    public static Observable<IChangeSet<T>> Sort<T, TKey>(
        this Observable<IChangeSet<T>> source,
        Func<T, TKey> keySelector,
        SortOptions options = SortOptions.None)
        where TKey : IComparable<TKey>
    {
        var comparer = Comparer<T>.Create((x, y) => keySelector(x).CompareTo(keySelector(y)));
        return new Sort<T>(source, comparer, options).Run();
    }

    public static Observable<IChangeSet<T>> Sort<T>(
        this Observable<IChangeSet<T>> source,
        Observable<IComparer<T>> comparerChanged,
        SortOptions options = SortOptions.None)
    {
        return source.Sort(Comparer<T>.Default, comparerChanged, options);
    }

    public static Observable<IChangeSet<T>> Sort<T>(
        this Observable<IChangeSet<T>> source,
        IComparer<T> initialComparer,
        Observable<IComparer<T>> comparerChanged,
        SortOptions options = SortOptions.None)
    {
        return new Sort<T>(source, initialComparer, options, comparerChanged: comparerChanged).Run();
    }

    public static Observable<IChangeSet<T>> Sort<T>(
        this Observable<IChangeSet<T>> source,
        IComparer<T> comparer,
        Observable<Unit> resorter,
        SortOptions options = SortOptions.None)
    {
        return new Sort<T>(source, comparer, options, resorter: resorter).Run();
    }

    public static IDisposable Bind<T>(
        this Observable<IChangeSet<T>> source,
        IList<T> target)
    {
        return source.Subscribe(changeSet =>
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        target.Insert(change.CurrentIndex, change.Item);
                        break;

                    case ListChangeReason.AddRange:
                        if (change.Range.Count > 0)
                        {
                            int idx = change.CurrentIndex;
                            foreach (var item in change.Range)
                            {
                                target.Insert(idx++, item);
                            }
                        }
                        else
                        {
                            target.Insert(change.CurrentIndex, change.Item);
                        }

                        break;

                    case ListChangeReason.Remove:
                        target.RemoveAt(change.CurrentIndex);
                        break;

                    case ListChangeReason.RemoveRange:
                        if (change.Range.Count > 0)
                        {
                            for (int i = 0; i < change.Range.Count; i++)
                            {
                                target.RemoveAt(change.CurrentIndex);
                            }
                        }
                        else
                        {
                            target.RemoveAt(change.CurrentIndex);
                        }

                        break;

                    case ListChangeReason.Replace:
                        target[change.CurrentIndex] = change.Item;
                        break;

                    case ListChangeReason.Moved:
                        var movedItem = target[change.PreviousIndex];
                        target.RemoveAt(change.PreviousIndex);
                        target.Insert(change.CurrentIndex, movedItem);
                        break;

                    case ListChangeReason.Clear:
                        target.Clear();
                        break;

                    case ListChangeReason.Refresh:
                        // No action needed; binding does not need to refresh
                        break;
                }
            }
        });
    }

    // Bind to IObservableCollection<T>
    public static IDisposable Bind<T>(
        this Observable<IChangeSet<T>> source,
        IObservableCollection<T> targetCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
    {
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }

    // Bind to out ReadOnlyObservableCollection<T>
    public static IDisposable Bind<T>(
        this Observable<IChangeSet<T>> source,
        out ReadOnlyObservableCollection<T> readOnlyObservableCollection,
        int resetThreshold = BindingOptions.DefaultResetThreshold)
    {
        var target = new ObservableCollectionExtended<T>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<T>(target);
        var options = new BindingOptions { ResetThreshold = resetThreshold };
        var adaptor = new ObservableCollectionAdaptor<T>(target, options);
        return source.Subscribe(changes => adaptor.Adapt(changes));
    }

    public static Observable<IChangeSet<Group<TKey, T>>> Group<T, TKey>(
        this Observable<IChangeSet<T>> source,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        return new Internal.GroupBy<T, TKey>(source, keySelector, keyComparer).Run();
    }

    public static Observable<IChangeSet<TValue>> DistinctValues<T, TValue>(
        this Observable<IChangeSet<T>> source,
        Func<T, TValue> valueSelector,
        IEqualityComparer<TValue>? comparer = null)
        where TValue : notnull
    {
        return new Internal.DistinctValues<T, TValue>(source, valueSelector, comparer).Run();
    }

    public static Observable<IChangeSet<TDestination>> TransformMany<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, IEnumerable<TDestination>> manySelector,
        IEqualityComparer<TDestination>? comparer = null)
        where TDestination : notnull
    {
        return new Internal.TransformMany<TSource, TDestination>(source, manySelector, comparer).Run();
    }

    public static Observable<IChangeSet<T>> Filter<T>(
        this Observable<IChangeSet<T>> source,
        Func<T, bool> predicate)
    {
        return new Internal.Filter<T>(source, predicate).Run();
    }

    public static Observable<IChangeSet<T>> Filter<T>(
        this Observable<IChangeSet<T>> source,
        Observable<Func<T, bool>> predicateChanged)
    {
        return new Internal.DynamicFilter<T>(source, predicateChanged).Run();
    }

    public static Observable<IChangeSet<T>> DisposeMany<T>(
        this Observable<IChangeSet<T>> source)
        where T : IDisposable
    {
        return new Internal.DisposeMany<T>(source).Run();
    }

    public static Observable<IChangeSet<T>> DisposeMany<T>(
        this Observable<IChangeSet<T>> source,
        Action<T> disposeAction)
        where T : notnull
    {
        return new Internal.DisposeMany<T>(source, disposeAction).Run();
    }

    public static Observable<IChangeSet<T>> SubscribeMany<T>(
        this Observable<IChangeSet<T>> source,
        Func<T, IDisposable> subscriptionFactory)
        where T : notnull
    {
        return new Internal.SubscribeMany<T>(source, subscriptionFactory).Run();
    }

    public static Observable<TDestination> MergeMany<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, Observable<TDestination>> selector)
        where TSource : notnull
    {
        return new Internal.MergeMany<TSource, TDestination>(source, selector).Run();
    }

    public static Observable<IChangeSet<TObject>> AutoRefresh<TObject>(
        this Observable<IChangeSet<TObject>> source,
        TimeSpan? changeSetBuffer = null,
        TimeSpan? propertyChangeThrottle = null,
        TimeProvider? timeProvider = null)
        where TObject : INotifyPropertyChanged
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenAnyPropertyChanged();
                }

                return t.WhenAnyPropertyChanged().Debounce(propertyChangeThrottle.Value, timeProvider ?? ObservableSystem.DefaultTimeProvider);
            },
            changeSetBuffer,
            timeProvider);
    }

    public static Observable<IChangeSet<TObject>> AutoRefresh<TObject, TProperty>(
        this Observable<IChangeSet<TObject>> source,
        Expression<Func<TObject, TProperty>> propertyAccessor,
        TimeSpan? changeSetBuffer = null,
        TimeSpan? propertyChangeThrottle = null,
        TimeProvider? timeProvider = null)
        where TObject : INotifyPropertyChanged
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (propertyAccessor is null)
        {
            throw new ArgumentNullException(nameof(propertyAccessor));
        }

        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenPropertyChanged(propertyAccessor, false);
                }

                return t.WhenPropertyChanged(propertyAccessor, false).Debounce(propertyChangeThrottle.Value, timeProvider ?? ObservableSystem.DefaultTimeProvider);
            },
            changeSetBuffer,
            timeProvider);
    }

    public static Observable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(
        this Observable<IChangeSet<TObject>> source,
        Func<TObject, Observable<TAny>> reevaluator,
        TimeSpan? changeSetBuffer = null,
        TimeProvider? timeProvider = null)
        where TObject : notnull
    {
        return new Internal.AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, timeProvider).Run();
    }

    // Additional operators

    /// <summary>
    /// Clones the target list as a side effect of the stream.
    /// </summary>
    public static Observable<IChangeSet<T>> Clone<T>(
        this Observable<IChangeSet<T>> source,
        IList<T> target)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return source.Do(changes =>
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        target.Insert(change.CurrentIndex, change.Item);
                        break;

                    case ListChangeReason.AddRange:
                        if (change.Range.Count > 0)
                        {
                            for (int i = 0; i < change.Range.Count; i++)
                            {
                                target.Insert(change.CurrentIndex + i, change.Range[i]);
                            }
                        }

                        break;

                    case ListChangeReason.Remove:
                        target.RemoveAt(change.CurrentIndex);
                        break;

                    case ListChangeReason.RemoveRange:
                        if (change.Range.Count > 0)
                        {
                            for (int i = 0; i < change.Range.Count; i++)
                            {
                                target.RemoveAt(change.CurrentIndex);
                            }
                        }

                        break;

                    case ListChangeReason.Replace:
                        target[change.CurrentIndex] = change.Item;
                        break;

                    case ListChangeReason.Moved:
                        var movedItem = target[change.PreviousIndex];
                        target.RemoveAt(change.PreviousIndex);
                        target.Insert(change.CurrentIndex, movedItem);
                        break;

                    case ListChangeReason.Clear:
                        target.Clear();
                        break;

                    case ListChangeReason.Refresh:
                        // No action needed
                        break;
                }
            }
        });
    }

    /// <summary>
    /// Cast the changes to another form.
    /// </summary>
    public static Observable<IChangeSet<TDestination>> Cast<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source)
        where TSource : notnull
        where TDestination : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(changes => (IChangeSet<TDestination>)new ChangeSet<TDestination>(
            changes.Select(change => CastChange<TSource, TDestination>(change, item => (TDestination)(object)item!))));
    }

    /// <summary>
    /// Cast the changes to another form using a conversion function.
    /// </summary>
    public static Observable<IChangeSet<TDestination>> Cast<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (conversionFactory is null)
        {
            throw new ArgumentNullException(nameof(conversionFactory));
        }

        return source.Select(changes => (IChangeSet<TDestination>)new ChangeSet<TDestination>(
            changes.Select(change => CastChange(change, conversionFactory))));
    }

    private static Change<TDestination> CastChange<TSource, TDestination>(
        Change<TSource> change,
        Func<TSource, TDestination> converter)
        where TSource : notnull
        where TDestination : notnull
    {
        return change.Reason switch
        {
            ListChangeReason.Add => new Change<TDestination>(change.Reason, converter(change.Item), change.CurrentIndex),
            ListChangeReason.AddRange => new Change<TDestination>(change.Reason, change.Range.Select(converter), change.CurrentIndex),
            ListChangeReason.Replace => new Change<TDestination>(change.Reason, converter(change.Item), change.PreviousItem != null ? converter(change.PreviousItem) : default, change.CurrentIndex),
            ListChangeReason.Remove => new Change<TDestination>(change.Reason, converter(change.Item), change.CurrentIndex),
            ListChangeReason.RemoveRange => new Change<TDestination>(change.Reason, change.Range.Select(converter), change.CurrentIndex),
            ListChangeReason.Moved => new Change<TDestination>(change.Reason, converter(change.Item), change.CurrentIndex, change.PreviousIndex),
            ListChangeReason.Refresh => Change<TDestination>.Refresh,
            ListChangeReason.Clear => new Change<TDestination>(change.Reason, change.Range.Select(converter), change.CurrentIndex),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    /// <summary>
    /// Defer the subscription until the stream has been inflated with data.
    /// </summary>
    public static Observable<IChangeSet<T>> DeferUntilLoaded<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Internal.DeferUntilLoaded<T>(source).Run();
    }

    /// <summary>
    /// Provides a call back for each item change.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ForEachChange<TObject>(
        this Observable<IChangeSet<TObject>> source,
        Action<Change<TObject>> action)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return source.Do(changes =>
        {
            foreach (var change in changes)
            {
                action(change);
            }
        });
    }

    /// <summary>
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    public static Observable<IChangeSet<T>> OnItemAdded<T>(
        this Observable<IChangeSet<T>> source,
        Action<T> addAction)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (addAction is null)
        {
            throw new ArgumentNullException(nameof(addAction));
        }

        return new Internal.OnBeingAdded<T>(source, addAction).Run();
    }

    /// <summary>
    /// Callback for each item as and when it is being removed from the stream.
    /// </summary>
    public static Observable<IChangeSet<T>> OnItemRemoved<T>(
        this Observable<IChangeSet<T>> source,
        Action<T> removeAction,
        bool invokeOnUnsubscribe = true)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (removeAction is null)
        {
            throw new ArgumentNullException(nameof(removeAction));
        }

        return new Internal.OnBeingRemoved<T>(source, removeAction, invokeOnUnsubscribe).Run();
    }

    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    public static Observable<IChangeSet<TObject>> OnItemRefreshed<TObject>(
        this Observable<IChangeSet<TObject>> source,
        Action<TObject> refreshAction)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (refreshAction is null)
        {
            throw new ArgumentNullException(nameof(refreshAction));
        }

        return source.Do(changes =>
        {
            foreach (var change in changes.Where(c => c.Reason == ListChangeReason.Refresh))
            {
                refreshAction(change.Item);
            }
        });
    }

    /// <summary>
    /// Defer the subscription until loaded and skip initial change set.
    /// </summary>
    public static Observable<IChangeSet<T>> SkipInitial<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    public static Observable<IChangeSet<T>> StartWithEmpty<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        return source.Prepend(ChangeSet<T>.Empty);
    }

    /// <summary>
    /// Suppress refresh notifications.
    /// </summary>
    public static Observable<IChangeSet<T>> SuppressRefresh<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        return source.WhereReasonsAreNot(ListChangeReason.Refresh);
    }

    /// <summary>
    /// Includes changes for the specified reasons only.
    /// </summary>
    public static Observable<IChangeSet<T>> WhereReasonsAre<T>(
        this Observable<IChangeSet<T>> source,
        params ListChangeReason[] reasons)
        where T : notnull
    {
        if (reasons is null)
        {
            throw new ArgumentNullException(nameof(reasons));
        }

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must select at least one reason");
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(changes => (IChangeSet<T>)new ChangeSet<T>(changes.Where(c => matches.Contains(c.Reason)))).NotEmpty();
    }

    /// <summary>
    /// Excludes updates for the specified reasons.
    /// </summary>
    public static Observable<IChangeSet<T>> WhereReasonsAreNot<T>(
        this Observable<IChangeSet<T>> source,
        params ListChangeReason[] reasons)
        where T : notnull
    {
        if (reasons is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must select at least one reason");
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(changes => (IChangeSet<T>)new ChangeSet<T>(changes.Where(c => !matches.Contains(c.Reason)))).NotEmpty();
    }

    /// <summary>
    /// Prevents an empty notification.
    /// </summary>
    public static Observable<IChangeSet<T>> NotEmpty<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(s => s.Count != 0);
    }

    /// <summary>
    /// Converts the change set into a fully formed collection. Each change in the source results in a new collection.
    /// </summary>
    public static Observable<IReadOnlyList<TObject>> ToCollection<TObject>(
        this Observable<IChangeSet<TObject>> source)
        where TObject : notnull
    {
        return source.QueryWhenChanged(items => items);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying after each modification to the underlying data.
    /// </summary>
    public static Observable<TDestination> QueryWhenChanged<TObject, TDestination>(
        this Observable<IChangeSet<TObject>> source,
        Func<IReadOnlyList<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (resultSelector is null)
        {
            throw new ArgumentNullException(nameof(resultSelector));
        }

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i) after each modification to the underlying data ii) upon subscription.
    /// </summary>
    public static Observable<IReadOnlyList<T>> QueryWhenChanged<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Internal.QueryWhenChanged<T>(source).Run();
    }

    /// <summary>
    /// List equivalent to Publish().RefCount(). The source is cached so long as there is at least 1 subscriber.
    /// </summary>
    public static Observable<IChangeSet<T>> RefCount<T>(
        this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Internal.RefCount<T>(source).Run();
    }

    /// <summary>
    /// Limits the size of the result set to the specified number of items.
    /// </summary>
    public static Observable<IChangeSet<T>> Top<T>(
        this Observable<IChangeSet<T>> source,
        int numberOfItems)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }

    /// <summary>
    /// Virtualises the source using parameters provided via the requests observable.
    /// </summary>
    public static Observable<IChangeSet<T>> Virtualise<T>(
        this Observable<IChangeSet<T>> source,
        Observable<VirtualRequest> requests)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        return new Internal.Virtualiser<T>(source, requests).Run();
    }

    /// <summary>
    /// Populates the source list into the destination list.
    /// </summary>
    public static IDisposable PopulateInto<T>(
        this Observable<IChangeSet<T>> source,
        ISourceList<T> destination)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        return source.Subscribe(changes =>
        {
            destination.Edit(updater =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            if (change.CurrentIndex >= 0)
                            {
                                updater.Insert(change.CurrentIndex, change.Item);
                            }
                            else
                            {
                                updater.Add(change.Item);
                            }

                            break;
                        case ListChangeReason.AddRange:
                            if (change.CurrentIndex >= 0)
                            {
                                updater.InsertRange(change.CurrentIndex, change.Range);
                            }
                            else
                            {
                                updater.AddRange(change.Range);
                            }

                            break;
                        case ListChangeReason.Replace:
                            updater.ReplaceAt(change.CurrentIndex, change.Item);
                            break;
                        case ListChangeReason.Remove:
                            updater.RemoveAt(change.CurrentIndex);
                            break;
                        case ListChangeReason.RemoveRange:
                            updater.RemoveRange(change.CurrentIndex, change.Range.Count);
                            break;
                        case ListChangeReason.Moved:
                            updater.Move(change.PreviousIndex, change.CurrentIndex);
                            break;
                        case ListChangeReason.Clear:
                            updater.Clear();
                            break;
                    }
                }
            });
        });
    }

    /// <summary>
    /// Apply a logical And operator between the collections. Items which are in all of the sources are included in the result.
    /// </summary>
    public static Observable<IChangeSet<T>> And<T>(
        this Observable<IChangeSet<T>> source,
        params Observable<IChangeSet<T>>[] others)
        where T : notnull
    {
        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.And, others);
    }

    /// <summary>
    /// Apply a logical Or operator between the collections. Items which are in any of the sources are included in the result.
    /// </summary>
    public static Observable<IChangeSet<T>> Or<T>(
        this Observable<IChangeSet<T>> source,
        params Observable<IChangeSet<T>>[] others)
        where T : notnull
    {
        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Or, others);
    }

    /// <summary>
    /// Apply a logical Except operator between the collections. Items which are in the source and not in the others are included in the result.
    /// </summary>
    public static Observable<IChangeSet<T>> Except<T>(
        this Observable<IChangeSet<T>> source,
        params Observable<IChangeSet<T>>[] others)
        where T : notnull
    {
        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Except, others);
    }

    /// <summary>
    /// Apply a logical Xor operator between the collections. Items which are only in one of the sources are included in the result.
    /// </summary>
    public static Observable<IChangeSet<T>> Xor<T>(
        this Observable<IChangeSet<T>> source,
        params Observable<IChangeSet<T>>[] others)
        where T : notnull
    {
        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Xor, others);
    }

    // Helper method for logical combinators
    private static Observable<IChangeSet<T>> Combine<T>(
        this Observable<IChangeSet<T>> source,
        CombineOperator type,
        params Observable<IChangeSet<T>>[] others)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (others is null)
        {
            throw new ArgumentNullException(nameof(others));
        }

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = new[] { source }.Concat(others).ToList();
        return new Internal.Combiner<T>(items, type).Run();
    }

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will be fired.
    /// </summary>
    public static Observable<IChangeSet<T>> BufferIf<T>(
        this Observable<IChangeSet<T>> source,
        Observable<bool> pauseIfTrueSelector)
        where T : notnull
    {
        return BufferIf(source, pauseIfTrueSelector, false, null);
    }

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will be fired.
    /// </summary>
    public static Observable<IChangeSet<T>> BufferIf<T>(
        this Observable<IChangeSet<T>> source,
        Observable<bool> pauseIfTrueSelector,
        bool initialPauseState)
        where T : notnull
    {
        return BufferIf(source, pauseIfTrueSelector, initialPauseState, null);
    }

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will be fired.
    /// </summary>
    public static Observable<IChangeSet<T>> BufferIf<T>(
        this Observable<IChangeSet<T>> source,
        Observable<bool> pauseIfTrueSelector,
        TimeSpan? timeOut)
        where T : notnull
    {
        return BufferIf(source, pauseIfTrueSelector, false, timeOut);
    }

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will be fired.
    /// </summary>
    public static Observable<IChangeSet<T>> BufferIf<T>(
        this Observable<IChangeSet<T>> source,
        Observable<bool> pauseIfTrueSelector,
        bool initialPauseState,
        TimeSpan? timeOut)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (pauseIfTrueSelector is null)
        {
            throw new ArgumentNullException(nameof(pauseIfTrueSelector));
        }

        return new Internal.BufferIf<T>(source, pauseIfTrueSelector, initialPauseState, timeOut).Run();
    }

    /// <summary>
    /// Buffers changes for an initial period only. After the period has elapsed, no further buffering occurs.
    /// </summary>
    public static Observable<IChangeSet<TObject>> BufferInitial<TObject>(
        this Observable<IChangeSet<TObject>> source,
        TimeSpan initialBuffer)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<IChangeSet<TObject>>(observer =>
        {
            var buffer = new List<IChangeSet<TObject>>();
            var hasEmitted = false;
            var timerDisposed = false;
            IDisposable? timerSubscription = null;
            IDisposable? sourceSubscription = null;

            void EmitBuffered()
            {
                if (buffer.Count > 0)
                {
                    var changes = new List<Change<TObject>>();
                    foreach (var cs in buffer)
                    {
                        changes.AddRange(cs);
                    }

                    observer.OnNext(new ChangeSet<TObject>(changes));
                    buffer.Clear();
                }

                hasEmitted = true;
            }

            sourceSubscription = source.DeferUntilLoaded().Subscribe(
                changes =>
                {
                    if (hasEmitted)
                    {
                        observer.OnNext(changes);
                    }
                    else
                    {
                        buffer.Add(changes);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            timerSubscription = Observable.Timer(initialBuffer).Subscribe(_ =>
            {
                if (!timerDisposed)
                {
                    EmitBuffered();
                }
            });

            return Disposable.Create(() =>
            {
                timerDisposed = true;
                timerSubscription?.Dispose();
                sourceSubscription?.Dispose();
            });
        });
    }

    /// <summary>
    /// Transforms an observable sequence of observable change sets into a single observable change set.
    /// </summary>
    public static Observable<IChangeSet<T>> Switch<T>(
        this Observable<Observable<IChangeSet<T>>> sources)
        where T : notnull
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        return new Internal.Switch<T>(sources).Run();
    }

    /// <summary>
    /// Converts an observable sequence of items into an observable change set.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<TObject> source)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Internal.ToObservableChangeSet<TObject>.Create(source, null, 0);
    }

    /// <summary>
    /// Converts an observable sequence of items into an observable change set, with size limit.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<TObject> source,
        int limitSizeTo)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Internal.ToObservableChangeSet<TObject>.Create(source, null, limitSizeTo);
    }

    /// <summary>
    /// Converts an observable sequence of items into an observable change set, with expiry.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<TObject> source,
        Func<TObject, TimeSpan?> expireAfter)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (expireAfter is null)
        {
            throw new ArgumentNullException(nameof(expireAfter));
        }

        return Internal.ToObservableChangeSet<TObject>.Create(source, expireAfter, 0);
    }

    /// <summary>
    /// Converts an observable sequence of collections into an observable change set.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<IEnumerable<TObject>> source)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Internal.ToObservableChangeSet<TObject>.CreateFromEnumerable(source, null, 0);
    }

    /// <summary>
    /// Converts an observable sequence of collections into an observable change set, with size limit.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<IEnumerable<TObject>> source,
        int limitSizeTo)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Internal.ToObservableChangeSet<TObject>.CreateFromEnumerable(source, null, limitSizeTo);
    }

    /// <summary>
    /// Converts an observable sequence of collections into an observable change set, with expiry.
    /// </summary>
    public static Observable<IChangeSet<TObject>> ToObservableChangeSet<TObject>(
        this Observable<IEnumerable<TObject>> source,
        Func<TObject, TimeSpan?> expireAfter)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (expireAfter is null)
        {
            throw new ArgumentNullException(nameof(expireAfter));
        }

        return Internal.ToObservableChangeSet<TObject>.CreateFromEnumerable(source, expireAfter, 0);
    }

    /// <summary>
    /// Removes index information from all changes in the stream (sets indices to -1).
    /// </summary>
    public static Observable<IChangeSet<T>> RemoveIndex<T>(this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(changes => (IChangeSet<T>)new ChangeSet<T>(changes.Select(RemoveIndexMapper)));

        static Change<T> RemoveIndexMapper(Change<T> change)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    return new Change<T>(ListChangeReason.Add, change.Item, -1);
                case ListChangeReason.AddRange:
                    return new Change<T>(ListChangeReason.AddRange, change.Range, -1);
                case ListChangeReason.Remove:
                    return new Change<T>(ListChangeReason.Remove, change.Item, -1);
                case ListChangeReason.RemoveRange:
                    return new Change<T>(ListChangeReason.RemoveRange, change.Range, -1);
                case ListChangeReason.Clear:
                    return new Change<T>(ListChangeReason.Clear, change.Range, -1);
                case ListChangeReason.Replace:
                    return new Change<T>(ListChangeReason.Replace, change.Item, change.PreviousItem, -1);
                case ListChangeReason.Moved:
                    return new Change<T>(ListChangeReason.Moved, change.Item, -1, -1);
                case ListChangeReason.Refresh:
                    return Change<T>.Refresh; // Already index agnostic
                default:
                    return change; // Fallback (should not hit)
            }
        }
    }

    /// <summary>
    /// Reverses the ordering of the change stream by maintaining an internal list and emitting the reversed view.
    /// NOTE: Simplified implementation always emits a Clear + AddRange diff after the initial AddRange.
    /// </summary>
    public static Observable<IChangeSet<T>> Reverse<T>(this Observable<IChangeSet<T>> source)
        where T : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var forward = new List<T>();
            var reversed = new List<T>();

            return source.Subscribe(changes =>
            {
                // Apply incoming changes to forward list
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            forward.Insert(change.CurrentIndex, change.Item);
                            break;
                        case ListChangeReason.AddRange:
                            forward.InsertRange(change.CurrentIndex, change.Range);
                            break;
                        case ListChangeReason.Remove:
                            forward.RemoveAt(change.CurrentIndex);
                            break;
                        case ListChangeReason.RemoveRange:
                            forward.RemoveRange(change.CurrentIndex, change.Range.Count);
                            break;
                        case ListChangeReason.Replace:
                            forward[change.CurrentIndex] = change.Item;
                            break;
                        case ListChangeReason.Moved:
                            var item = forward[change.PreviousIndex];
                            forward.RemoveAt(change.PreviousIndex);
                            forward.Insert(change.CurrentIndex, item);
                            break;
                        case ListChangeReason.Clear:
                            forward.Clear();
                            break;
                        case ListChangeReason.Refresh:
                            // No structural change
                            break;
                    }
                }

                var newReversed = forward.AsEnumerable().Reverse().ToList();

                var diff = new ChangeSet<T>();
                if (reversed.Count == 0 && newReversed.Count > 0)
                {
                    diff.Add(new Change<T>(ListChangeReason.AddRange, newReversed, 0));
                }
                else if (newReversed.Count == 0 && reversed.Count > 0)
                {
                    diff.Add(new Change<T>(ListChangeReason.Clear, reversed, -1));
                }
                else if (!newReversed.SequenceEqual(reversed))
                {
                    // Simplified: emit clear + add range
                    if (reversed.Count > 0)
                    {
                        diff.Add(new Change<T>(ListChangeReason.Clear, reversed, -1));
                    }
                    if (newReversed.Count > 0)
                    {
                        diff.Add(new Change<T>(ListChangeReason.AddRange, newReversed, 0));
                    }
                }

                reversed = newReversed;
                if (diff.Count > 0)
                {
                    observer.OnNext(diff);
                }
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }
}
