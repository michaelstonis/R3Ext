// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class Sort<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly IComparer<T> _comparer;
    private readonly SortOptions _options;
    private readonly Observable<IComparer<T>>? _comparerChanged;
    private readonly Observable<Unit>? _resorter;

    public Sort(
        Observable<IChangeSet<T>> source,
        IComparer<T> comparer,
        SortOptions options = SortOptions.None,
        Observable<IComparer<T>>? comparerChanged = null,
        Observable<Unit>? resorter = null)
    {
        _source = source;
        _comparer = comparer;
        _options = options;
        _comparerChanged = comparerChanged;
        _resorter = resorter;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var sortedList = new ChangeAwareList<T>();
            var currentComparer = _comparer;
            var disposables = new CompositeDisposable();

            // Subscribe to source changes
            _source.Subscribe(
                changes =>
                {
                    try
                    {
                        ProcessChanges(sortedList, changes, currentComparer);
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

            // Subscribe to comparer changes if provided
            if (_comparerChanged != null)
            {
                _comparerChanged.Subscribe(newComparer =>
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
            }

            // Subscribe to resort trigger if provided
            if (_resorter != null)
            {
                _resorter.Subscribe(_ =>
                {
                    try
                    {
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
            }

            return disposables;
        });
    }

    private void ProcessChanges(ChangeAwareList<T> sortedList, IChangeSet<T> changes, IComparer<T> comparer)
    {
        // Optimize initial load: if target is empty and incoming are only adds, bulk-add in sorted order
        if (sortedList.Count == 0)
        {
            bool onlyAdds = true;
            foreach (var c in changes)
            {
                if (c.Reason != ListChangeReason.Add && c.Reason != ListChangeReason.AddRange)
                {
                    onlyAdds = false;
                    break;
                }
            }

            if (onlyAdds)
            {
                var initialItems = new List<T>();
                foreach (var c in changes)
                {
                    if (c.Reason == ListChangeReason.Add)
                    {
                        initialItems.Add(c.Item);
                    }
                    else if (c.Reason == ListChangeReason.AddRange)
                    {
                        if (c.Range.Count > 0)
                        {
                            initialItems.AddRange(c.Range);
                        }
                        else
                        {
                            // Our SourceList emits AddRange per item; pull from Item
                            initialItems.Add(c.Item);
                        }
                    }
                }

                if (initialItems.Count > 0)
                {
                    var ordered = initialItems.OrderBy(x => x, comparer).ToList();
                    foreach (var item in ordered)
                    {
                        sortedList.Add(item);
                    }

                    return;
                }
            }
        }

        // First, collect all AddRange items to sort them before inserting
        var addRangeItems = new List<T>();
        foreach (var c in changes)
        {
            if (c.Reason == ListChangeReason.AddRange)
            {
                if (c.Range.Count > 0)
                {
                    addRangeItems.AddRange(c.Range);
                }
                else
                {
                    addRangeItems.Add(c.Item);
                }
            }
        }

        if (addRangeItems.Count > 0)
        {
            // Sort the items first
            var sorted = addRangeItems.OrderBy(x => x, comparer).ToList();

            // Insert each sorted item at its correct position (list might not be empty)
            if (sortedList.Count == 0)
            {
                sortedList.AddRange(sorted);
            }
            else
            {
                // Insert each sorted item at its correct position
                foreach (var item in sorted)
                {
                    Insert(sortedList, item, comparer);
                }
            }
        }

        // Process other changes
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    Insert(sortedList, change.Item, comparer);
                    break;

                case ListChangeReason.AddRange:
                    // Already handled above
                    break;

                case ListChangeReason.Remove:
                    Remove(sortedList, change.Item, comparer);
                    break;

                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        foreach (var item in change.Range)
                        {
                            Remove(sortedList, item, comparer);
                        }
                    }
                    else
                    {
                        Remove(sortedList, change.Item, comparer);
                    }

                    break;

                case ListChangeReason.Replace:
                    if (change.PreviousItem != null)
                    {
                        Remove(sortedList, change.PreviousItem, comparer);
                    }

                    Insert(sortedList, change.Item, comparer);

                    break;

                case ListChangeReason.Moved:
                    // Item is already in the correct position relative to sort
                    // No action needed as moves don't affect sorted order
                    break;

                case ListChangeReason.Clear:
                    sortedList.Clear();

                    break;

                case ListChangeReason.Refresh:
                    // Re-sort on refresh
                    Resort(sortedList, comparer);

                    break;
            }
        }
    }

    private void Insert(ChangeAwareList<T> sortedList, T item, IComparer<T> comparer)
    {
        if (sortedList.Count == 0)
        {
            sortedList.Add(item);
            return;
        }

        var index = FindInsertIndex(sortedList, item, comparer);
        sortedList.Insert(index, item);
    }

    private void Remove(ChangeAwareList<T> sortedList, T item, IComparer<T> comparer)
    {
        var index = FindItemIndex(sortedList, item, comparer);
        if (index >= 0)
        {
            sortedList.RemoveAt(index);
        }
    }

    private int FindInsertIndex(ChangeAwareList<T> sortedList, T item, IComparer<T> comparer)
    {
        if (_options.HasFlag(SortOptions.UseBinarySearch))
        {
            var index = BinarySearch(sortedList, item, comparer);
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

    private int FindItemIndex(ChangeAwareList<T> sortedList, T item, IComparer<T> comparer)
    {
        if (_options.HasFlag(SortOptions.UseBinarySearch))
        {
            var index = BinarySearch(sortedList, item, comparer);
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
                    if (EqualityComparer<T>.Default.Equals(sortedList[i], item))
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

    private int BinarySearch(ChangeAwareList<T> sortedList, T item, IComparer<T> comparer)
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

    private void Resort(ChangeAwareList<T> sortedList, IComparer<T> comparer)
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
