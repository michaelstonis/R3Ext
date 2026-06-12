// Port of DynamicData to R3.
namespace R3Ext.DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Func<TObject, TKey, Observable<TDestination>> _observableSelector;

    public TransformOnObservable(
        Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, Observable<TDestination>> observableSelector)
    {
        _source = source;
        _observableSelector = observableSelector;
    }

    public Observable<IChangeSet<TDestination, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
        {
            // key → (latestDestination, subscription)
            var subscriptions = new Dictionary<TKey, IDisposable>();
            var values = new Dictionary<TKey, TDestination>();

            void Subscribe(TKey key, TObject item)
            {
                var obs = _observableSelector(item, key);
                var sub = obs.Subscribe(
                    dest =>
                    {
                        var cs = new ChangeSet<TDestination, TKey>();
                        if (values.TryGetValue(key, out var prev))
                        {
                            // Already have a value: emit Update
                            cs.Add(new Change<TDestination, TKey>(Kernel.ChangeReason.Update, key, dest, prev));
                        }
                        else
                        {
                            // First emission: emit Add
                            cs.Add(new Change<TDestination, TKey>(Kernel.ChangeReason.Add, key, dest));
                        }

                        values[key] = dest;
                        observer.OnNext(cs);
                    },
                    observer.OnErrorResume,
                    static _ => { });

                subscriptions[key] = sub;
            }

            void Unsubscribe(TKey key)
            {
                if (subscriptions.TryGetValue(key, out var sub))
                {
                    sub.Dispose();
                    subscriptions.Remove(key);
                }
            }

            var sourceSubscription = _source.Subscribe(
                changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:
                                Subscribe(change.Key, change.Current);
                                break;

                            case Kernel.ChangeReason.Update:
                                // Dispose old subscription (keep existing output value if any; new obs will overwrite)
                                Unsubscribe(change.Key);
                                Subscribe(change.Key, change.Current);
                                break;

                            case Kernel.ChangeReason.Remove:
                                Unsubscribe(change.Key);
                                if (values.TryGetValue(change.Key, out var removedDest))
                                {
                                    values.Remove(change.Key);
                                    var cs = new ChangeSet<TDestination, TKey>();
                                    cs.Add(new Change<TDestination, TKey>(Kernel.ChangeReason.Remove, change.Key, removedDest, removedDest));
                                    observer.OnNext(cs);
                                }

                                break;

                            case Kernel.ChangeReason.Refresh:
                                // No structural change; per-item observable still running
                                break;
                        }
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();
                foreach (var sub in subscriptions.Values)
                {
                    sub.Dispose();
                }

                subscriptions.Clear();
                values.Clear();
            });
        });
    }
}
