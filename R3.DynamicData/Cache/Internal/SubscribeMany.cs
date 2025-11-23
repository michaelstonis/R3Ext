// Port of DynamicData to R3.

// Style suppression pragmas for internal operator.
#pragma warning disable SA1116, SA1513, SA1516, SA1503, SA1127, SA1210
namespace R3.DynamicData.Cache.Internal;

internal sealed class SubscribeMany<TObject, TKey>
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Func<TObject, IDisposable> _subscriptionFactory;

    public SubscribeMany(Observable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
    {
        _source = source;
        _subscriptionFactory = subscriptionFactory;
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        var state = new SubscribeManyState<TObject, TKey>(_source, _subscriptionFactory);
        return Observable.Create<IChangeSet<TObject, TKey>, SubscribeManyState<TObject, TKey>>(
            state,
            static (observer, state) =>
            {
                var locker = new object();
                var disposables = new CompositeDisposable();
                var subscriptions = new Dictionary<TKey, IDisposable>();

                state.Source.Subscribe(changes =>
                {
                    lock (locker)
                    {
                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case Kernel.ChangeReason.Add:
                                    if (!subscriptions.ContainsKey(change.Key))
                                    {
                                        subscriptions[change.Key] = state.SubscriptionFactory(change.Current);
                                    }
                                    break;
                                case Kernel.ChangeReason.Update:
                                    // Dispose previous subscription (object updated) then create new one.
                                    if (subscriptions.TryGetValue(change.Key, out var existing))
                                    {
                                        existing.Dispose();
                                    }
                                    subscriptions[change.Key] = state.SubscriptionFactory(change.Current);
                                    break;
                                case Kernel.ChangeReason.Remove:
                                    if (subscriptions.TryGetValue(change.Key, out var sub))
                                    {
                                        sub.Dispose();
                                        subscriptions.Remove(change.Key);
                                    }
                                    break;
                                case Kernel.ChangeReason.Refresh:
                                    // Refresh keeps same object; ensure subscription exists but do not recreate.
                                    if (!subscriptions.ContainsKey(change.Key))
                                    {
                                        subscriptions[change.Key] = state.SubscriptionFactory(change.Current);
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
                        foreach (var sub in subscriptions.Values)
                        {
                            sub.Dispose();
                        }
                        subscriptions.Clear();
                    }
                });
            });
    }

    private readonly struct SubscribeManyState<TObj, TK>
        where TK : notnull
    {
        public readonly Observable<IChangeSet<TObj, TK>> Source;
        public readonly Func<TObj, IDisposable> SubscriptionFactory;

        public SubscribeManyState(Observable<IChangeSet<TObj, TK>> source, Func<TObj, IDisposable> subscriptionFactory)
        {
            Source = source;
            SubscriptionFactory = subscriptionFactory;
        }
    }
}
