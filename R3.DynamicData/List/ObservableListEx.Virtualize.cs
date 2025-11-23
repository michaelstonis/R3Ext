// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.Cache;

public static partial class ObservableListEx
{
    /// <summary>
    /// Virtualizes a sorted list by exposing only a windowed view of the data.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="virtualRequests">Observable that emits virtualization requests.</param>
    /// <returns>An observable that emits windowed changesets.</returns>
    public static Observable<IChangeSet<T>> Virtualize<T>(
        this Observable<IChangeSet<T>> source,
        Observable<VirtualRequest> virtualRequests)
        where T : notnull
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var fullList = new List<T>();
            var currentWindow = new VirtualRequest(0, 10);
            VirtualRequest? previousWindow = null;
            var disposables = new CompositeDisposable();

            // Subscribe to source changes
            source.Subscribe(
                changes =>
                {
                    try
                    {
                        // Apply changes to full list
                        ApplyChangesToFullList(fullList, changes);

                        // Emit windowed changes
                        var windowedChanges = CreateWindowedChangeset(fullList, previousWindow, currentWindow);
                        if (windowedChanges.Count > 0)
                        {
                            observer.OnNext(windowedChanges);
                        }

                        previousWindow = currentWindow;
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disposables);

            // Subscribe to virtual requests
            virtualRequests.Subscribe(
                request =>
                {
                    try
                    {
                        previousWindow = currentWindow;
                        currentWindow = request;

                        // Emit changes for new window
                        var windowedChanges = CreateWindowedChangeset(fullList, previousWindow, currentWindow);
                        if (windowedChanges.Count > 0)
                        {
                            observer.OnNext(windowedChanges);
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

    private static void ApplyChangesToFullList<T>(List<T> fullList, IChangeSet<T> changes)
        where T : notnull
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex <= fullList.Count)
                    {
                        fullList.Insert(change.CurrentIndex, change.Item);
                    }
                    else
                    {
                        fullList.Add(change.Item);
                    }

                    break;

                case ListChangeReason.Remove:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < fullList.Count)
                    {
                        fullList.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        fullList.Remove(change.Item);
                    }

                    break;

                case ListChangeReason.Replace:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < fullList.Count)
                    {
                        fullList[change.CurrentIndex] = change.Item;
                    }

                    break;

                case ListChangeReason.Moved:
                    if (change.PreviousIndex >= 0 && change.PreviousIndex < fullList.Count)
                    {
                        var item = fullList[change.PreviousIndex];
                        fullList.RemoveAt(change.PreviousIndex);
                        if (change.CurrentIndex >= 0 && change.CurrentIndex <= fullList.Count)
                        {
                            fullList.Insert(change.CurrentIndex, item);
                        }
                    }

                    break;

                case ListChangeReason.Clear:
                    fullList.Clear();
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        if (change.CurrentIndex >= 0)
                        {
                            fullList.InsertRange(change.CurrentIndex, change.Range);
                        }
                        else
                        {
                            fullList.AddRange(change.Range);
                        }
                    }

                    break;

                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0 && change.CurrentIndex >= 0)
                    {
                        fullList.RemoveRange(change.CurrentIndex, change.Range.Count);
                    }

                    break;

                case ListChangeReason.Refresh:
                    // Refresh doesn't change the list, just signal downstream
                    break;
            }
        }
    }

    private static ChangeSet<T> CreateWindowedChangeset<T>(
        List<T> fullList,
        VirtualRequest? previousWindow,
        VirtualRequest currentWindow)
        where T : notnull
    {
        var changeset = new ChangeSet<T>();

        // Calculate actual window bounds
        var currStart = Math.Min(currentWindow.StartIndex, fullList.Count);
        var currEnd = Math.Min(currStart + currentWindow.Size, fullList.Count);

        // If no previous window, just add all items in the current window (initial emission)
        if (previousWindow == null)
        {
            for (int i = currStart; i < currEnd; i++)
            {
                if (i < fullList.Count)
                {
                    changeset.Add(new Change<T>(ListChangeReason.Add, fullList[i], i - currStart));
                }
            }

            return changeset;
        }

        var prevStart = Math.Min(previousWindow.Value.StartIndex, fullList.Count);
        var prevEnd = Math.Min(prevStart + previousWindow.Value.Size, fullList.Count);

        // If windows are the same, no changes
        if (prevStart == currStart && prevEnd == currEnd)
        {
            return changeset;
        }

        // Simple case: Clear and re-add all items in new window
        if (prevStart != currStart || (prevEnd - prevStart) != (currEnd - currStart))
        {
            // Remove all items from previous window
            for (int i = prevEnd - 1; i >= prevStart; i--)
            {
                if (i < fullList.Count)
                {
                    changeset.Add(new Change<T>(ListChangeReason.Remove, fullList[i], i - prevStart));
                }
            }

            // Add all items in new window
            for (int i = currStart; i < currEnd; i++)
            {
                if (i < fullList.Count)
                {
                    changeset.Add(new Change<T>(ListChangeReason.Add, fullList[i], i - currStart));
                }
            }
        }

        return changeset;
    }

    /// <summary>
    /// Creates a paging observable that emits page-sized windows.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="pageRequests">Observable that emits page numbers (0-indexed).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>An observable that emits windowed changesets.</returns>
    public static Observable<IChangeSet<T>> Page<T>(
        this Observable<IChangeSet<T>> source,
        Observable<int> pageRequests,
        int pageSize = 25)
        where T : notnull
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var virtualRequests = pageRequests.Select(page => new VirtualRequest(page * pageSize, pageSize));
        return source.Virtualize(virtualRequests);
    }
}
