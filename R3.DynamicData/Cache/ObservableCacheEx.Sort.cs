// Port of DynamicData to R3.

using R3.DynamicData.Kernel;
using R3.DynamicData.List;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extension methods for observable cache change sets.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Sorts cache changes by the specified comparer, maintaining sorted order through updates.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="comparer">The comparer to determine sort order.</param>
    /// <param name="options">Sort options (e.g., use binary search).</param>
    /// <returns>An observable that emits sorted list changesets.</returns>
    public static Observable<IChangeSet<TObject>> Sort<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        SortOptions options = SortOptions.None)
        where TObject : notnull
        where TKey : notnull
    {
        return Observable.Create<IChangeSet<TObject>>(observer =>
        {
            var sortedList = new ChangeAwareList<TObject>();
            var keyMap = new Dictionary<TKey, TObject>();

            return source.Subscribe(
                changes =>
                {
                    try
                    {
                        ProcessCacheChanges(sortedList, keyMap, changes, comparer, options);
                        var outputChanges = sortedList.CaptureChanges();

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
        });
    }

    /// <summary>
    /// Sorts cache changes by a property selector, maintaining sorted order through updates.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSortKey">The type of the property to sort by.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="keySelector">Function to extract the property to sort by.</param>
    /// <param name="options">Sort options (e.g., use binary search).</param>
    /// <returns>An observable that emits sorted list changesets.</returns>
    public static Observable<IChangeSet<TObject>> Sort<TObject, TKey, TSortKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TSortKey> keySelector,
        SortOptions options = SortOptions.None)
        where TObject : notnull
        where TKey : notnull
        where TSortKey : IComparable<TSortKey>
    {
        var comparer = Comparer<TObject>.Create((x, y) => keySelector(x).CompareTo(keySelector(y)));
        return Sort(source, comparer, options);
    }

    /// <summary>
    /// Sorts cache changes with dynamic comparer updates.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable cache.</param>
    /// <param name="comparer">The initial comparer.</param>
    /// <param name="comparerChanged">Observable that emits new comparers to trigger re-sorting.</param>
    /// <param name="options">Sort options.</param>
    /// <returns>An observable that emits sorted list changesets.</returns>
    public static Observable<IChangeSet<TObject>> Sort<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        Observable<IComparer<TObject>> comparerChanged,
        SortOptions options = SortOptions.None)
        where TObject : notnull
        where TKey : notnull
    {
        return Observable.Create<IChangeSet<TObject>>(observer =>
        {
            var sortedList = new ChangeAwareList<TObject>();
            var keyMap = new Dictionary<TKey, TObject>();
            var currentComparer = comparer;
            var disposables = new CompositeDisposable();

            source.Subscribe(
                changes =>
                {
                    try
                    {
                        ProcessCacheChanges(sortedList, keyMap, changes, currentComparer, options);
                        var outputChanges = sortedList.CaptureChanges();

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
                observer.OnCompleted).AddTo(disposables);

            comparerChanged.Subscribe(newComparer =>
            {
                try
                {
                    currentComparer = newComparer;
                    Resort(sortedList, currentComparer);
                    var outputChanges = sortedList.CaptureChanges();
                    if (outputChanges.Count > 0)
                    {
                        observer.OnNext(outputChanges);
                    }
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }).AddTo(disposables);

            return disposables;
        });
    }

    private static void ProcessCacheChanges<TObject, TKey>(
        ChangeAwareList<TObject> sortedList,
        Dictionary<TKey, TObject> keyMap,
        IChangeSet<TObject, TKey> changes,
        IComparer<TObject> comparer,
        SortOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    keyMap[change.Key] = change.Current;
                    InsertSorted(sortedList, change.Current, comparer, options);
                    break;

                case ChangeReason.Update:
                    // Remove old value, insert new value
                    if (keyMap.TryGetValue(change.Key, out var oldValue))
                    {
                        RemoveSorted(sortedList, oldValue, comparer, options);
                    }

                    keyMap[change.Key] = change.Current;
                    InsertSorted(sortedList, change.Current, comparer, options);
                    break;

                case ChangeReason.Remove:
                    if (keyMap.TryGetValue(change.Key, out var removeValue))
                    {
                        RemoveSorted(sortedList, removeValue, comparer, options);
                        keyMap.Remove(change.Key);
                    }

                    break;

                case ChangeReason.Refresh:
                    // On refresh, re-sort the entire list
                    Resort(sortedList, comparer);
                    break;
            }
        }
    }

    private static void InsertSorted<TObject>(
        ChangeAwareList<TObject> sortedList,
        TObject item,
        IComparer<TObject> comparer,
        SortOptions options)
        where TObject : notnull
    {
        if (sortedList.Count == 0)
        {
            sortedList.Add(item);
            return;
        }

        var index = FindInsertIndex(sortedList, item, comparer, options);
        sortedList.Insert(index, item);
    }

    private static void RemoveSorted<TObject>(
        ChangeAwareList<TObject> sortedList,
        TObject item,
        IComparer<TObject> comparer,
        SortOptions options)
        where TObject : notnull
    {
        var index = FindItemIndex(sortedList, item, comparer, options);
        if (index >= 0)
        {
            sortedList.RemoveAt(index);
        }
    }

    private static int FindInsertIndex<TObject>(
        ChangeAwareList<TObject> sortedList,
        TObject item,
        IComparer<TObject> comparer,
        SortOptions options)
        where TObject : notnull
    {
        if (options.HasFlag(SortOptions.UseBinarySearch))
        {
            var index = BinarySearchSorted(sortedList, item, comparer);
            return index < 0 ? ~index : index;
        }

        // Linear search
        for (int i = 0; i < sortedList.Count; i++)
        {
            if (comparer.Compare(item, sortedList[i]) <= 0)
            {
                return i;
            }
        }

        return sortedList.Count;
    }

    private static int FindItemIndex<TObject>(
        ChangeAwareList<TObject> sortedList,
        TObject item,
        IComparer<TObject> comparer,
        SortOptions options)
        where TObject : notnull
    {
        if (options.HasFlag(SortOptions.UseBinarySearch))
        {
            var index = BinarySearchSorted(sortedList, item, comparer);
            if (index >= 0)
            {
                // Binary search found a match, but there might be duplicates
                // Scan for exact item reference
                while (index > 0 && comparer.Compare(sortedList[index - 1], item) == 0)
                {
                    index--;
                }

                for (int i = index; i < sortedList.Count && comparer.Compare(sortedList[i], item) == 0; i++)
                {
                    if (EqualityComparer<TObject>.Default.Equals(sortedList[i], item))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        // Linear search
        return sortedList.IndexOf(item);
    }

    private static int BinarySearchSorted<TObject>(
        ChangeAwareList<TObject> sortedList,
        TObject item,
        IComparer<TObject> comparer)
        where TObject : notnull
    {
        int left = 0;
        int right = sortedList.Count - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) / 2);
            int comparison = comparer.Compare(sortedList[mid], item);

            if (comparison == 0)
            {
                return mid;
            }
            else if (comparison < 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return ~left;
    }

    private static void Resort<TObject>(
        ChangeAwareList<TObject> sortedList,
        IComparer<TObject> comparer)
        where TObject : notnull
    {
        var items = sortedList.ToList();
        sortedList.Clear();
        if (items.Count == 0)
        {
            return;
        }

        var ordered = items.OrderBy(x => x, comparer).ToList();
        sortedList.AddRange(ordered);
    }
}
