// Combined Sort + Bind operator for cache side (inspired by DynamicData SortAndBind).
// Provides lower allocation and avoids transmitting full sorted state via intermediate sorted change sets.

#pragma warning disable SA1503 // Braces should not be omitted
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1116 // Parameters should begin on the line after the declaration when spanning multiple lines
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1514 // Element documentation header should be preceded by blank line
#pragma warning disable SA1127 // Generic type constraints should be on their own line
#pragma warning disable SA1413 // Use trailing comma in multi-line initializers
#pragma warning disable SA1107 // Code should not contain multiple statements on one line

using System.Collections.ObjectModel;
using R3.DynamicData.Binding;
using R3.DynamicData.Kernel;
using R3.DynamicData.List;
using R3.DynamicData.List.Internal;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Sorts cache items and binds directly to a read-only observable collection.
    /// </summary>
    public static IDisposable SortAndBind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnly,
        IComparer<TObject> comparer,
        SortAndBindOptions? options = null)
        where TObject : notnull where TKey : notnull
    {
        var opt = options ?? new SortAndBindOptions();
        // ObservableCollection has no capacity ctor; ignore InitialCapacity except for potential future optimization.
        var target = new ObservableCollectionExtended<TObject>();
        readOnly = new ReadOnlyObservableCollection<TObject>(target);
        return source.SortAndBindInternal(target, comparer, opt);
    }

    /// <summary>
    /// Sorts cache items and binds directly into provided observable collection.
    /// </summary>
    public static IDisposable SortAndBind<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IObservableCollection<TObject> target,
        IComparer<TObject> comparer,
        SortAndBindOptions? options = null)
        where TObject : notnull where TKey : notnull
    {
        return source.SortAndBindInternal(target, comparer, options ?? new SortAndBindOptions());
    }

    private static IDisposable SortAndBindInternal<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IObservableCollection<TObject> target,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull where TKey : notnull
    {
        // Maintain sorted list as ChangeAwareList for efficient diff capture when resets not needed.
        var sorted = new ChangeAwareList<TObject>();
        var keyMap = new Dictionary<TKey, TObject>();
        int pendingChangeCount = 0;

        void RebuildAll()
        {
            target.Clear();
            foreach (var item in sorted)
            {
                target.Add(item);
            }
            pendingChangeCount = 0;
        }

        void ApplySortedDiff(IChangeSet<TObject> diffs)
        {
            foreach (var change in diffs)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        target.Insert(change.CurrentIndex, change.Item);
                        break;
                    case ListChangeReason.Remove:
                        target.RemoveAt(change.CurrentIndex);
                        break;
                    case ListChangeReason.Moved:
                        var mv = target[change.PreviousIndex];
                        target.RemoveAt(change.PreviousIndex);
                        target.Insert(change.CurrentIndex, mv);
                        break;
                    case ListChangeReason.Replace:
                        if (options.UseReplaceForUpdates)
                        {
                            target[change.CurrentIndex] = change.Item;
                        }
                        else
                        {
                            target.RemoveAt(change.CurrentIndex);
                            target.Insert(change.CurrentIndex, change.Item);
                        }
                        break;
                    case ListChangeReason.Clear:
                        target.Clear();
                        break;
                }
            }
        }

        return source.Subscribe(changes =>
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        keyMap[change.Key] = change.Current;
                        InsertSorted(sorted, change.Current, comparer, options.UseBinarySearch);
                        pendingChangeCount++;
                        break;
                    case ChangeReason.Update:
                        if (keyMap.TryGetValue(change.Key, out var old))
                        {
                            RemoveSorted(sorted, old, comparer, options.UseBinarySearch);
                        }
                        keyMap[change.Key] = change.Current;
                        InsertSorted(sorted, change.Current, comparer, options.UseBinarySearch);
                        pendingChangeCount++;
                        break;
                    case ChangeReason.Remove:
                        if (keyMap.TryGetValue(change.Key, out var rem))
                        {
                            RemoveSorted(sorted, rem, comparer, options.UseBinarySearch);
                            keyMap.Remove(change.Key);
                            pendingChangeCount++;
                        }
                        break;
                    case ChangeReason.Refresh:
                        // Refresh: re-sort entire list; treat as potential bulk.
                        ResortAll(sorted, comparer);
                        pendingChangeCount = options.ResetThreshold; // force rebuild decision below.
                        break;
                }
            }

            // Decide reset vs diff.
            if (pendingChangeCount >= options.ResetThreshold)
            {
                RebuildAll();
            }
            else
            {
                var diffs = sorted.CaptureChanges();
                if (diffs.Count > 0)
                {
                    ApplySortedDiff(diffs);
                }
            }
        });
    }

    private static void InsertSorted<T>(ChangeAwareList<T> list, T item, IComparer<T> comparer, bool useBinary)
        where T : notnull
    {
        if (list.Count == 0)
        {
            list.Add(item);
            return;
        }
        int index;
        if (useBinary)
        {
            index = BinarySearch(list, item, comparer);
            if (index < 0) index = ~index;
        }
        else
        {
            index = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (comparer.Compare(item, list[i]) <= 0)
                {
                    index = i;
                    break;
                }
            }
        }
        list.Insert(index, item);
    }

    private static void RemoveSorted<T>(ChangeAwareList<T> list, T item, IComparer<T> comparer, bool useBinary)
        where T : notnull
    {
        int index;
        if (useBinary)
        {
            index = BinarySearch(list, item, comparer);
            if (index >= 0)
            {
                // Walk to exact reference (handles equal comparer values).
                while (index > 0 && comparer.Compare(list[index - 1], item) == 0) index--;
                for (int i = index; i < list.Count && comparer.Compare(list[i], item) == 0; i++)
                {
                    if (EqualityComparer<T>.Default.Equals(list[i], item))
                    {
                        list.RemoveAt(i);
                        return;
                    }
                }
            }
        }
        else
        {
            index = list.IndexOf(item);
            if (index >= 0) list.RemoveAt(index);
        }
    }

    private static int BinarySearch<T>(ChangeAwareList<T> list, T item, IComparer<T> comparer)
        where T : notnull
    {
        int lo = 0; int hi = list.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            int cmp = comparer.Compare(list[mid], item);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    // Rename to ResortAll to avoid clash with existing Resort in ObservableCacheEx.Sort.cs
    private static void ResortAll<T>(ChangeAwareList<T> list, IComparer<T> comparer)
        where T : notnull
    {
        var snapshot = list.ToList();
        snapshot.Sort(comparer);
        list.Clear();
        list.AddRange(snapshot);
    }
}
