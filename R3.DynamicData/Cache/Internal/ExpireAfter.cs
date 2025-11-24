// Port of DynamicData to R3.
namespace R3.DynamicData.Cache.Internal;

internal sealed class ExpireAfter<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Func<TObject, TimeSpan?> _expireSelector;
    private readonly TimeProvider _timeProvider;

    public ExpireAfter(
        Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TimeSpan?> expireSelector,
        TimeProvider? timeProvider = null)
    {
        _source = source;
        _expireSelector = expireSelector;
        _timeProvider = timeProvider ?? ObservableSystem.DefaultTimeProvider;
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var items = new Dictionary<TKey, TObject>();
            var timers = new Dictionary<TKey, IDisposable>();
            var disp = new CompositeDisposable();
            var gate = new object();

            void ScheduleExpiration(TKey key, TObject item)
            {
                // Cancel existing timer
                if (timers.TryGetValue(key, out var existing))
                {
                    existing.Dispose();
                    timers.Remove(key);
                }

                var span = _expireSelector(item);
                if (!span.HasValue || span.Value <= TimeSpan.Zero)
                {
                    return; // no expiration
                }

                // Use timer observable
                var timerObs = Observable.Timer(span.Value, _timeProvider);
                var sub = timerObs.Subscribe(
                    _ =>
                    {
                        lock (gate)
                        {
                            if (items.TryGetValue(key, out var current))
                            {
                                // Emit removal change set
                                var cs = new ChangeSet<TObject, TKey>();
                                cs.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, current, current));
                                items.Remove(key);
                                timers.Remove(key);
                                observer.OnNext(cs);
                            }
                        }
                    });
                timers[key] = sub.AddTo(disp);
            }

            _source.Subscribe(
                changes =>
                {
                    lock (gate)
                    {
                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case Kernel.ChangeReason.Add:
                                    items[change.Key] = change.Current;
                                    ScheduleExpiration(change.Key, change.Current);
                                    break;
                                case Kernel.ChangeReason.Update:
                                    items[change.Key] = change.Current;
                                    ScheduleExpiration(change.Key, change.Current);
                                    break;
                                case Kernel.ChangeReason.Remove:
                                    if (items.Remove(change.Key))
                                    {
                                        if (timers.TryGetValue(change.Key, out var t))
                                        {
                                            t.Dispose();
                                            timers.Remove(change.Key);
                                        }
                                    }

                                    break;
                                case Kernel.ChangeReason.Refresh:

                                    // Re-evaluate expiration on refresh
                                    if (items.TryGetValue(change.Key, out var refreshed))
                                    {
                                        ScheduleExpiration(change.Key, refreshed);
                                    }

                                    break;
                            }
                        }
                    }

                    observer.OnNext(changes);
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disp);

            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    foreach (var t in timers.Values)
                    {
                        t.Dispose();
                    }

                    timers.Clear();
                    items.Clear();
                }

                disp.Dispose();
            });
        });
    }
}
