// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class SubscribeMany<T>
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Func<T, IDisposable> _subscriptionFactory;

    public SubscribeMany(Observable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
    {
        _source = source;
        _subscriptionFactory = subscriptionFactory;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var locker = new object();
            var disposables = new CompositeDisposable();
            var subscriptions = new Dictionary<T, IDisposable>();

            _source.Subscribe(
                changes =>
            {
                lock (locker)
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                            case ListChangeReason.Refresh:
                                if (change.Item != null && !subscriptions.ContainsKey(change.Item))
                                {
                                    subscriptions[change.Item] = _subscriptionFactory(change.Item);
                                }

                                break;

                            case ListChangeReason.AddRange:
                                if (change.Range.Count > 0)
                                {
                                    foreach (var item in change.Range)
                                    {
                                        if (item != null && !subscriptions.ContainsKey(item))
                                        {
                                            subscriptions[item] = _subscriptionFactory(item);
                                        }
                                    }
                                }
                                else if (change.Item != null && !subscriptions.ContainsKey(change.Item))
                                {
                                    subscriptions[change.Item] = _subscriptionFactory(change.Item);
                                }

                                break;

                            case ListChangeReason.Remove:
                                if (change.Item != null && subscriptions.TryGetValue(change.Item, out var subscription))
                                {
                                    subscription.Dispose();
                                    subscriptions.Remove(change.Item);
                                }

                                break;

                            case ListChangeReason.RemoveRange:
                                if (change.Range.Count > 0)
                                {
                                    foreach (var item in change.Range)
                                    {
                                        if (item != null && subscriptions.TryGetValue(item, out var sub))
                                        {
                                            sub.Dispose();
                                            subscriptions.Remove(item);
                                        }
                                    }
                                }
                                else if (change.Item != null && subscriptions.TryGetValue(change.Item, out var sub2))
                                {
                                    sub2.Dispose();
                                    subscriptions.Remove(change.Item);
                                }

                                break;

                            case ListChangeReason.Clear:
                                // Dispose all subscriptions on clear
                                foreach (var sub in subscriptions.Values)
                                {
                                    sub.Dispose();
                                }

                                subscriptions.Clear();
                                break;

                            case ListChangeReason.Replace:
                                if (change.PreviousItem != null && subscriptions.TryGetValue(change.PreviousItem, out var oldSubscription))
                                {
                                    oldSubscription.Dispose();
                                    subscriptions.Remove(change.PreviousItem);
                                }

                                if (change.Item != null && !subscriptions.ContainsKey(change.Item))
                                {
                                    subscriptions[change.Item] = _subscriptionFactory(change.Item);
                                }

                                break;
                        }
                    }

                    observer.OnNext(changes);
                }
            }, observer.OnErrorResume, observer.OnCompleted).AddTo(disposables);

            return Disposable.Create(() =>
            {
                disposables.Dispose();
                lock (locker)
                {
                    foreach (var subscription in subscriptions.Values)
                    {
                        subscription.Dispose();
                    }

                    subscriptions.Clear();
                }
            });
        });
    }
}
