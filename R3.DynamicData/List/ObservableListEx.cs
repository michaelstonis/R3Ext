// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

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
                        {
                            var item = target[change.PreviousIndex];
                            target.RemoveAt(change.PreviousIndex);
                            target.Insert(change.CurrentIndex, item);
                        }

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
}
