// Copyright (c) 2025 Michael Stonis. All rights reserved.
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
        var comparer = Comparer<T>.Default;
        return new Sort<T>(source, comparer, options, comparerChanged: comparerChanged).Run();
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
}