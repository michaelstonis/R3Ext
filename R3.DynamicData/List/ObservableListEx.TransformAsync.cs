// Port of DynamicData to R3.

namespace R3.DynamicData.List;

public static partial class ObservableListEx
{
    /// <summary>
    /// Asynchronously transforms items in the observable list using a task-based selector.
    /// When an item is removed before its transformation completes, the task is cancelled.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="transformFactory">Function that creates a transformation task for each item.</param>
    /// <returns>An observable that emits change sets with transformed items.</returns>
    public static Observable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, Task<TDestination>> transformFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        return TransformAsync(source, (item, _) => transformFactory(item));
    }

    /// <summary>
    /// Asynchronously transforms items in the observable list using a task-based selector with cancellation support.
    /// When an item is removed before its transformation completes, the cancellation token is triggered.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="transformFactory">Function that creates a transformation task for each item with cancellation.</param>
    /// <returns>An observable that emits change sets with transformed items.</returns>
    public static Observable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, CancellationToken, Task<TDestination>> transformFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        return Observable.Create<IChangeSet<TDestination>>(observer =>
        {
            var transformations = new Dictionary<TSource, PendingTransformation<TSource, TDestination>>();
            var completedItems = new List<TransformedItem<TSource, TDestination>>();
            var gate = new object();

            return source.Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                        case ListChangeReason.Replace:
                            HandleAddOrReplace(
                                change,
                                transformFactory,
                                transformations,
                                completedItems,
                                observer,
                                gate);
                            break;

                        case ListChangeReason.Remove:
                            HandleRemove(
                                change.Item,
                                transformations,
                                completedItems,
                                observer,
                                gate);
                            break;

                        case ListChangeReason.AddRange:
                            foreach (var item in change.Range)
                            {
                                var addChange = new Change<TSource>(ListChangeReason.Add, item, -1);
                                HandleAddOrReplace(
                                    addChange,
                                    transformFactory,
                                    transformations,
                                    completedItems,
                                    observer,
                                    gate);
                            }

                            break;

                        case ListChangeReason.RemoveRange:
                            foreach (var item in change.Range)
                            {
                                HandleRemove(item, transformations, completedItems, observer, gate);
                            }

                            break;

                        case ListChangeReason.Clear:
                            foreach (var transformation in transformations.Values)
                            {
                                transformation.Cts.Cancel();
                                transformation.Cts.Dispose();
                            }

                            transformations.Clear();

                            if (completedItems.Count > 0)
                            {
                                var clearedItems = completedItems.Select(x => x.Destination).ToList();
                                completedItems.Clear();
                                observer.OnNext(
                                    new ChangeSet<TDestination>(
                                        new[]
                                        {
                                            new Change<TDestination>(ListChangeReason.Clear, clearedItems, 0),
                                        }));
                            }

                            break;

                        case ListChangeReason.Moved:
                            // For moved items, if transformation is complete, emit a move for the destination
                            var movedItem = completedItems.FirstOrDefault(x => EqualityComparer<TSource>.Default.Equals(x.Source, change.Item));
                            if (movedItem != null)
                            {
                                var oldIndex = completedItems.IndexOf(movedItem);
                                if (oldIndex >= 0)
                                {
                                    completedItems.RemoveAt(oldIndex);
                                    var newIndex = Math.Min(change.CurrentIndex, completedItems.Count);
                                    completedItems.Insert(newIndex, movedItem);

                                    observer.OnNext(
                                        new ChangeSet<TDestination>(
                                            new[]
                                            {
                                                new Change<TDestination>(
                                                    ListChangeReason.Moved,
                                                    movedItem.Destination,
                                                    newIndex,
                                                    oldIndex),
                                            }));
                                }
                            }

                            break;
                    }
                }
            });
        });
    }

    private static void HandleAddOrReplace<TSource, TDestination>(
        Change<TSource> change,
        Func<TSource, CancellationToken, Task<TDestination>> transformFactory,
        Dictionary<TSource, PendingTransformation<TSource, TDestination>> transformations,
        List<TransformedItem<TSource, TDestination>> completedItems,
        Observer<IChangeSet<TDestination>> observer,
        object gate)
        where TSource : notnull
        where TDestination : notnull
    {
        var item = change.Item;
        var isReplace = change.Reason == ListChangeReason.Replace;

        // Cancel existing transformation if this is a replace
        if (transformations.TryGetValue(item, out var existingTransformation))
        {
            existingTransformation.Cts.Cancel();
            existingTransformation.Cts.Dispose();
            transformations.Remove(item);
        }

        // Check if item already exists in completed items (for Replace)
        var existingCompletedIndex = -1;
        TDestination? previousDestination = default;

        if (isReplace)
        {
            // For Replace, look up by index in the original change
            if (change.CurrentIndex >= 0 && change.CurrentIndex < completedItems.Count)
            {
                existingCompletedIndex = change.CurrentIndex;
                previousDestination = completedItems[existingCompletedIndex].Destination;
            }
        }

        // Start new transformation
        var cts = new CancellationTokenSource();
        var pending = new PendingTransformation<TSource, TDestination>(item, cts);
        transformations[item] = pending;

        // Execute transformation
        Task.Run(async () =>
        {
            try
            {
                var result = await transformFactory(item, cts.Token);

                // Check if still valid (not cancelled)
                if (!cts.Token.IsCancellationRequested)
                {
                    lock (gate)
                    {
                        if (isReplace && existingCompletedIndex >= 0)
                        {
                            // Replace at specific index
                            completedItems[existingCompletedIndex] = new TransformedItem<TSource, TDestination>(item, result);

                            observer.OnNext(
                                new ChangeSet<TDestination>(
                                    new[]
                                    {
                                        new Change<TDestination>(
                                            ListChangeReason.Replace,
                                            result,
                                            previousDestination,
                                            existingCompletedIndex),
                                    }));
                        }
                        else
                        {
                            // Add
                            var transformedItem = new TransformedItem<TSource, TDestination>(item, result);
                            completedItems.Add(transformedItem);

                            observer.OnNext(
                                new ChangeSet<TDestination>(
                                    new[]
                                    {
                                        new Change<TDestination>(
                                            ListChangeReason.Add,
                                            result,
                                            completedItems.Count - 1),
                                    }));
                        }

                        // Clean up from pending
                        transformations.Remove(item);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled, do nothing
            }
            catch (Exception ex)
            {
                // Silently ignore transformation errors
                // Real errors should be handled by the transform factory
                _ = ex;
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    private static void HandleRemove<TSource, TDestination>(
        TSource item,
        Dictionary<TSource, PendingTransformation<TSource, TDestination>> transformations,
        List<TransformedItem<TSource, TDestination>> completedItems,
        Observer<IChangeSet<TDestination>> observer,
        object gate)
        where TSource : notnull
        where TDestination : notnull
    {
        lock (gate)
        {
            // Cancel pending transformation
            if (transformations.TryGetValue(item, out var transformation))
            {
                transformation.Cts.Cancel();
                transformation.Cts.Dispose();
                transformations.Remove(item);
            }

            // Remove from completed items - only if transformation was complete
            var completedItem = completedItems.FirstOrDefault(x => x != null && EqualityComparer<TSource>.Default.Equals(x.Source, item));
            if (completedItem != null)
            {
                var index = completedItems.IndexOf(completedItem);
                completedItems.RemoveAt(index);

                observer.OnNext(
                    new ChangeSet<TDestination>(
                        new[]
                        {
                            new Change<TDestination>(
                                ListChangeReason.Remove,
                                completedItem.Destination,
                                index),
                        }));
            }
        }
    }

    private class PendingTransformation<TSource, TDestination>
    {
        public TSource Source { get; }

        public CancellationTokenSource Cts { get; }

        public PendingTransformation(TSource source, CancellationTokenSource cts)
        {
            Source = source;
            Cts = cts;
        }
    }

    private class TransformedItem<TSource, TDestination>
    {
        public TSource Source { get; }

        public TDestination Destination { get; }

        public TransformedItem(TSource source, TDestination destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
