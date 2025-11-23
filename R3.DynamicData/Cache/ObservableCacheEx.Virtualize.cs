// Port of DynamicData to R3.

using System.Collections.Generic;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Virtualizes a cache by exposing only a windowed view of the data.
    /// The cache must be sorted for meaningful virtualization.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects.</typeparam>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="virtualRequests">Observable that emits virtualization requests.</param>
    /// <returns>An observable that emits windowed changesets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Virtualize<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<VirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var cache = new Dictionary<TKey, TObject>();
            var sortedKeys = new List<TKey>();
            var windowedKeys = new HashSet<TKey>();
            VirtualRequest? currentWindow = null;
            var disposables = new CompositeDisposable();

            void EmitWindowedChanges()
            {
                try
                {
                    if (!currentWindow.HasValue)
                    {
                        return; // Don't emit until we have a virtual request
                    }

                    var changeset = new ChangeSet<TObject, TKey>();
                    var newWindowedKeys = new HashSet<TKey>();

                    // Calculate actual window bounds
                    var start = Math.Min(currentWindow.Value.StartIndex, sortedKeys.Count);
                    var end = Math.Min(start + currentWindow.Value.Size, sortedKeys.Count);

                    // Collect keys in the new window
                    for (int i = start; i < end; i++)
                    {
                        newWindowedKeys.Add(sortedKeys[i]);
                    }

                    // Remove items no longer in window
                    foreach (var key in windowedKeys)
                    {
                        if (!newWindowedKeys.Contains(key) && cache.ContainsKey(key))
                        {
                            changeset.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, cache[key]));
                        }
                    }

                    // Add new items in window
                    foreach (var key in newWindowedKeys)
                    {
                        if (!windowedKeys.Contains(key) && cache.ContainsKey(key))
                        {
                            changeset.Add(new Change<TObject, TKey>(ChangeReason.Add, key, cache[key]));
                        }
                    }

                    windowedKeys = newWindowedKeys;

                    if (changeset.Count > 0)
                    {
                        observer.OnNext(changeset);
                    }
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            // Subscribe to source changes
            source.Subscribe(
                changes =>
                {
                    try
                    {
                        var hasChanges = false;

                        // Apply changes to cache and sorted keys
                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                    cache[change.Key] = change.Current;
                                    sortedKeys.Add(change.Key);
                                    hasChanges = true;
                                    break;

                                case ChangeReason.Update:
                                    cache[change.Key] = change.Current;
                                    hasChanges = true;
                                    break;

                                case ChangeReason.Remove:
                                    cache.Remove(change.Key);
                                    sortedKeys.Remove(change.Key);
                                    hasChanges = true;
                                    break;

                                case ChangeReason.Refresh:
                                    // Refresh doesn't change structure, but might affect windowing
                                    hasChanges = true;
                                    break;
                            }
                        }

                        if (hasChanges)
                        {
                            EmitWindowedChanges();
                        }
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
                    currentWindow = request;
                    EmitWindowedChanges();
                }).AddTo(disposables);

            return disposables;
        });
    }

    /// <summary>
    /// Creates a paging observable that emits page-sized windows from a cache.
    /// The cache must be sorted for meaningful pagination.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects.</typeparam>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="pageRequests">Observable that emits page numbers (0-indexed).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>An observable that emits windowed changesets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Page<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<int> pageRequests,
        int pageSize = 25)
        where TObject : notnull
        where TKey : notnull
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero");
        }

        var virtualRequests = pageRequests.Select(page => new VirtualRequest(page * pageSize, pageSize));
        return source.Virtualize(virtualRequests);
    }

    /// <summary>
    /// Limits the cache result set to the specified number of items.
    /// The cache must be sorted for meaningful top-N selection.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects.</typeparam>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="numberOfItems">The maximum number of items to include.</param>
    /// <returns>An observable that emits changesets limited to the top N items.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Top<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        int numberOfItems)
        where TObject : notnull
        where TKey : notnull
    {
        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items must be greater than zero");
        }

        return source.Virtualize(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }
}
