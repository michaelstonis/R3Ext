// Port of DynamicData to R3.
namespace R3.DynamicData.Cache.Internal;

internal sealed class FilterOnObservable<TObject, TKey, TBool>
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Func<TObject, Observable<TBool>> _observableSelector;
    private readonly IEqualityComparer<TObject> _comparer;

    private sealed class Slot
    {
        public TObject Item = default!;
        public bool Included;
    }

    public FilterOnObservable(
        Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, Observable<TBool>> observableSelector,
        IEqualityComparer<TObject>? comparer = null)
    {
        _source = source;
        _observableSelector = observableSelector;
        _comparer = comparer ?? EqualityComparer<TObject>.Default;
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var slots = new Dictionary<TKey, Slot>();
            var subscriptions = new Dictionary<TKey, IDisposable>();
            var disp = new CompositeDisposable();

            void EvaluateEmission(TKey key, bool isIncluded, TObject item)
            {
                if (!slots.TryGetValue(key, out var slot))
                {
                    slot = new Slot { Item = item, Included = false };
                    slots[key] = slot;
                }

                if (isIncluded && !slot.Included)
                {
                    slot.Included = true;
                    var cs = new ChangeSet<TObject, TKey>();
                    cs.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, item));
                    observer.OnNext(cs);
                }
                else if (!isIncluded && slot.Included)
                {
                    slot.Included = false;
                    var cs = new ChangeSet<TObject, TKey>();
                    cs.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, item, item));
                    observer.OnNext(cs);
                }
            }

            _source.Subscribe(
                changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case Kernel.ChangeReason.Add:

                                // establish subscription
                                var observable = _observableSelector(change.Current);
                                subscriptions[change.Key] = observable.Subscribe(val => EvaluateEmission(change.Key, Convert.ToBoolean(val), change.Current));
                                break;
                            case Kernel.ChangeReason.Update:

                                // replace subscription
                                if (subscriptions.TryGetValue(change.Key, out var old))
                                {
                                    old.Dispose();
                                }

                                var obs = _observableSelector(change.Current);
                                subscriptions[change.Key] = obs.Subscribe(val => EvaluateEmission(change.Key, Convert.ToBoolean(val), change.Current));
                                if (slots.TryGetValue(change.Key, out var slot))
                                {
                                    slot.Item = change.Current;
                                }

                                break;
                            case Kernel.ChangeReason.Remove:
                                if (subscriptions.TryGetValue(change.Key, out var sub))
                                {
                                    sub.Dispose();
                                    subscriptions.Remove(change.Key);
                                }

                                if (slots.TryGetValue(change.Key, out var s) && s.Included)
                                {
                                    var cs = new ChangeSet<TObject, TKey>();
                                    cs.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, change.Key, change.Current, change.Current));
                                    observer.OnNext(cs);
                                }

                                slots.Remove(change.Key);
                                break;
                            case Kernel.ChangeReason.Refresh:

                                // no structural change; keep existing inclusion state
                                break;
                        }
                    }

                    observer.OnNext(changes); // propagate original changes downstream
                },
                observer.OnErrorResume,
                observer.OnCompleted).AddTo(disp);

            return Disposable.Create(() =>
            {
                foreach (var sub in subscriptions.Values)
                {
                    sub.Dispose();
                }

                subscriptions.Clear();
                disp.Dispose();
            });
        });
    }
}
