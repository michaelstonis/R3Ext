// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// Extension methods for observable list change sets.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Filters the observable list based on per-item observable predicates.
    /// Each item's inclusion is determined by an observable boolean stream.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="predicateSelector">Function that creates an observable predicate for each item.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<T>> FilterOnObservable<T>(
        this Observable<IChangeSet<T>> source,
        Func<T, Observable<bool>> predicateSelector)
        where T : notnull
    {
        return Observable.Create<IChangeSet<T>, FilterOnObservableState<T>>(
            new FilterOnObservableState<T>(source, predicateSelector),
            static (observer, state) =>
            {
                var trackedItems = new Dictionary<T, TrackedItem<T>>();
                var includedItems = new List<T>();
                var disposables = new CompositeDisposable();

                state.Source.Subscribe(
                    (observer, trackedItems, includedItems, state),
                    static (changeSet, tuple) =>
                    {
                        try
                        {
                            var outputChanges = new ChangeSet<T>();

                            foreach (var change in changeSet)
                            {
                                switch (change.Reason)
                                {
                                    case ListChangeReason.Add:
                                    case ListChangeReason.Replace:
                                        HandleAddOrReplace(
                                            change.Item,
                                            change.CurrentIndex,
                                            tuple.trackedItems,
                                            tuple.includedItems,
                                            tuple.state.PredicateSelector,
                                            outputChanges,
                                            tuple.observer);
                                        break;

                                    case ListChangeReason.Remove:
                                        HandleRemove(
                                            change.Item,
                                            change.CurrentIndex,
                                            tuple.trackedItems,
                                            tuple.includedItems,
                                            outputChanges);
                                        break;

                                    case ListChangeReason.AddRange:
                                        foreach (var item in change.Range)
                                        {
                                            HandleAddOrReplace(
                                                item,
                                                -1,
                                                tuple.trackedItems,
                                                tuple.includedItems,
                                                tuple.state.PredicateSelector,
                                                outputChanges,
                                                tuple.observer);
                                        }

                                        break;

                                    case ListChangeReason.RemoveRange:
                                        foreach (var item in change.Range)
                                        {
                                            HandleRemove(item, -1, tuple.trackedItems, tuple.includedItems, outputChanges);
                                        }

                                        break;

                                    case ListChangeReason.Clear:
                                        foreach (var trackedValue in tuple.trackedItems.Values)
                                        {
                                            trackedValue.Subscription?.Dispose();
                                        }

                                        tuple.trackedItems.Clear();
                                        if (tuple.includedItems.Count > 0)
                                        {
                                            var itemsToRemove = tuple.includedItems.ToList();
                                            tuple.includedItems.Clear();
                                            outputChanges.Add(new Change<T>(ListChangeReason.Clear, itemsToRemove, 0));
                                        }

                                        break;

                                    case ListChangeReason.Moved:
                                        // For moved items, we need to update their position in includedItems
                                        var movedItem = change.Item;
                                        if (tuple.trackedItems.TryGetValue(movedItem, out var tracked) && tracked.IsIncluded)
                                        {
                                            var oldIndex = tuple.includedItems.IndexOf(movedItem);
                                            if (oldIndex >= 0)
                                            {
                                                tuple.includedItems.RemoveAt(oldIndex);
                                                var newIndex = Math.Min(change.CurrentIndex, tuple.includedItems.Count);
                                                tuple.includedItems.Insert(newIndex, movedItem);
                                                outputChanges.Add(new Change<T>(
                                                    ListChangeReason.Moved,
                                                    movedItem,
                                                    newIndex,
                                                    oldIndex));
                                            }
                                        }

                                        break;
                                }
                            }

                            if (outputChanges.Count > 0)
                            {
                                tuple.observer.OnNext(outputChanges);
                            }
                        }
                        catch (Exception ex)
                        {
                            tuple.observer.OnErrorResume(ex);
                        }
                    },
                    static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                    static (result, tuple) =>
                    {
                        if (result.IsSuccess)
                        {
                            tuple.observer.OnCompleted();
                        }
                        else
                        {
                            tuple.observer.OnCompleted(result);
                        }
                    }).AddTo(disposables);

                return Disposable.Create((trackedItems, includedItems, disposables), static tuple =>
                {
                    foreach (var trackedValue in tuple.trackedItems.Values)
                    {
                        trackedValue.Subscription?.Dispose();
                    }

                    tuple.trackedItems.Clear();
                    tuple.includedItems.Clear();
                    tuple.disposables.Dispose();
                });
            });
    }

    private static void HandleAddOrReplace<T>(
        T item,
        int index,
        Dictionary<T, TrackedItem<T>> trackedItems,
        List<T> includedItems,
        Func<T, Observable<bool>> predicateSelector,
        ChangeSet<T> outputChanges,
        Observer<IChangeSet<T>> observer)
        where T : notnull
    {
        // Dispose existing subscription if replacing
        if (trackedItems.TryGetValue(item, out var existing))
        {
            existing.Subscription?.Dispose();
            if (existing.IsIncluded)
            {
                var removeIndex = includedItems.IndexOf(item);
                if (removeIndex >= 0)
                {
                    includedItems.RemoveAt(removeIndex);
                    outputChanges.Add(new Change<T>(ListChangeReason.Remove, item, removeIndex));
                }
            }
        }

        // Create new tracked item
        var tracked = new TrackedItem<T> { Item = item };
        trackedItems[item] = tracked;

        // Subscribe to predicate observable
        tracked.Subscription = predicateSelector(item).Subscribe(isIncluded =>
        {
            try
            {
                var wasIncluded = tracked.IsIncluded;
                tracked.IsIncluded = isIncluded;

                var changes = new ChangeSet<T>();

                if (isIncluded && !wasIncluded)
                {
                    // Item now passes filter - add it
                    includedItems.Add(item);
                    changes.Add(new Change<T>(ListChangeReason.Add, item, includedItems.Count - 1));
                }
                else if (!isIncluded && wasIncluded)
                {
                    // Item no longer passes filter - remove it
                    var removeIndex = includedItems.IndexOf(item);
                    if (removeIndex >= 0)
                    {
                        includedItems.RemoveAt(removeIndex);
                        changes.Add(new Change<T>(ListChangeReason.Remove, item, removeIndex));
                    }
                }

                if (changes.Count > 0)
                {
                    observer.OnNext(changes);
                }
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        });
    }

    private static void HandleRemove<T>(
        T item,
        int index,
        Dictionary<T, TrackedItem<T>> trackedItems,
        List<T> includedItems,
        ChangeSet<T> outputChanges)
        where T : notnull
    {
        if (trackedItems.TryGetValue(item, out var tracked))
        {
            tracked.Subscription?.Dispose();
            trackedItems.Remove(item);

            if (tracked.IsIncluded)
            {
                var removeIndex = includedItems.IndexOf(item);
                if (removeIndex >= 0)
                {
                    includedItems.RemoveAt(removeIndex);
                    outputChanges.Add(new Change<T>(ListChangeReason.Remove, item, removeIndex));
                }
            }
        }
    }

    private class TrackedItem<T>
        where T : notnull
    {
        public T Item { get; set; } = default!;

        public bool IsIncluded { get; set; }

        public IDisposable? Subscription { get; set; }
    }

    private readonly struct FilterOnObservableState<T>
        where T : notnull
    {
        public readonly Observable<IChangeSet<T>> Source;
        public readonly Func<T, Observable<bool>> PredicateSelector;

        public FilterOnObservableState(Observable<IChangeSet<T>> source, Func<T, Observable<bool>> predicateSelector)
        {
            Source = source;
            PredicateSelector = predicateSelector;
        }
    }
}
