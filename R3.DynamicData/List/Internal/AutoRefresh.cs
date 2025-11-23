// Port of DynamicData to R3.

using System.Collections.Generic;
using System.Linq;
using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class AutoRefresh<TObject, TAny>
    where TObject : notnull
{
    private readonly Observable<IChangeSet<TObject>> _source;
    private readonly Func<TObject, Observable<TAny>> _reEvaluator;
    private readonly TimeSpan? _buffer;
    private readonly TimeProvider? _timeProvider;

    public AutoRefresh(Observable<IChangeSet<TObject>> source, Func<TObject, Observable<TAny>> reEvaluator, TimeSpan? buffer = null, TimeProvider? timeProvider = null)
    {
        _source = source;
        _reEvaluator = reEvaluator;
        _buffer = buffer;
        _timeProvider = timeProvider;
    }

    public Observable<IChangeSet<TObject>> Run()
    {
        return Observable.Defer(() =>
        {
            var currentList = new List<TObject>();
            var itemSubscriptions = new Dictionary<TObject, IDisposable>();

            var refreshSubject = new Subject<TObject>();

            // Create buffered refresh stream if needed
            var bufferedRefreshes = ApplyBufferIfNeeded(refreshSubject);

            // Convert individual item refreshes to changesets with proper indices
            var refreshChangeSets = bufferedRefreshes
                .Select(item =>
                {
                    var index = currentList.IndexOf(item);
                    if (index >= 0)
                    {
                        var refreshChange = new Change<TObject>(ListChangeReason.Refresh, item, index);
                        return (IChangeSet<TObject>)new ChangeSet<TObject>(new[] { refreshChange });
                    }

                    return (IChangeSet<TObject>)new ChangeSet<TObject>(Array.Empty<Change<TObject>>());
                });

            // Process source changesets and manage subscriptions
            var processedSource = _source
                .Do(changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                currentList.Insert(change.CurrentIndex, change.Current);
                                SubscribeToItem(change.Current, itemSubscriptions, refreshSubject);
                                break;

                            case ListChangeReason.AddRange:
                                var rangeItems = change.Range.ToList();
                                currentList.InsertRange(change.CurrentIndex, rangeItems);
                                foreach (var rangeItem in rangeItems)
                                {
                                    SubscribeToItem(rangeItem, itemSubscriptions, refreshSubject);
                                }

                                break;

                            case ListChangeReason.Replace:
                                UnsubscribeFromItem(currentList[change.CurrentIndex], itemSubscriptions);
                                currentList[change.CurrentIndex] = change.Current;
                                SubscribeToItem(change.Current, itemSubscriptions, refreshSubject);
                                break;

                            case ListChangeReason.Remove:
                                UnsubscribeFromItem(currentList[change.CurrentIndex], itemSubscriptions);
                                currentList.RemoveAt(change.CurrentIndex);
                                break;

                            case ListChangeReason.RemoveRange:
                                for (int i = 0; i < change.Range.Count; i++)
                                {
                                    UnsubscribeFromItem(currentList[change.CurrentIndex], itemSubscriptions);
                                    currentList.RemoveAt(change.CurrentIndex);
                                }

                                break;

                            case ListChangeReason.Moved:
                                var movedItem = currentList[change.PreviousIndex];
                                currentList.RemoveAt(change.PreviousIndex);
                                currentList.Insert(change.CurrentIndex, movedItem);
                                break;

                            case ListChangeReason.Clear:
                                foreach (var clearItem in currentList)
                                {
                                    UnsubscribeFromItem(clearItem, itemSubscriptions);
                                }

                                currentList.Clear();
                                break;
                        }
                    }
                });

            // Merge original changesets with refresh changesets
            return processedSource.Merge(refreshChangeSets);
        });
    }

    private void SubscribeToItem(TObject item, Dictionary<TObject, IDisposable> subscriptions, Subject<TObject> refreshSubject)
    {
        if (!subscriptions.ContainsKey(item))
        {
            var subscription = _reEvaluator(item).Subscribe(_ => refreshSubject.OnNext(item));
            subscriptions[item] = subscription;
        }
    }

    private void UnsubscribeFromItem(TObject item, Dictionary<TObject, IDisposable> subscriptions)
    {
        if (subscriptions.TryGetValue(item, out var subscription))
        {
            subscription.Dispose();
            subscriptions.Remove(item);
        }
    }

    private Observable<TObject> ApplyBufferIfNeeded(Observable<TObject> source)
    {
        if (_buffer.HasValue)
        {
            var timeProvider = _timeProvider ?? ObservableSystem.DefaultTimeProvider;
            return source.Debounce(_buffer.Value, timeProvider);
        }

        return source;
    }
}
