// Port of DynamicData to R3.

// Style suppression pragmas for internal operator.
#pragma warning disable SA1116, SA1513, SA1516, SA1503, SA1127, SA1210
using System.ComponentModel;

namespace R3.DynamicData.Cache.Internal;

internal sealed class AutoRefresh<TObject, TAny>
    where TObject : notnull
{
    private readonly Observable<IChangeSet<TObject, object>> _source; // generic key typed later via wrapper
    private readonly Func<TObject, Observable<TAny>> _reEvaluator;
    private readonly TimeSpan? _buffer;
    private readonly TimeProvider? _timeProvider;

    public AutoRefresh(Observable<IChangeSet<TObject, object>> source,
        Func<TObject, Observable<TAny>> reEvaluator,
        TimeSpan? buffer = null,
        TimeProvider? timeProvider = null)
    {
        _source = source;
        _reEvaluator = reEvaluator;
        _buffer = buffer;
        _timeProvider = timeProvider;
    }

    public Observable<IChangeSet<TObject, object>> Run()
    {
        // Stream of refresh-only changes triggered by reevaluator observables.
        var refreshes = _source
            .Select(changeSet =>
            {
                var itemObservables = changeSet
                    .Where(c => c.Reason == Kernel.ChangeReason.Add || c.Reason == Kernel.ChangeReason.Update)
                    .Select(c => _reEvaluator(c.Current).Select(_ => c.Key));
                return itemObservables.Merge();
            })
            .Merge()
            .Select(key =>
            {
                // Current object for key looked up from latest source snapshot (maintained below).
                // We rely on latest known items dictionary updated in side-effect.
                return key;
            });

        var bufferedRefreshKeys = ApplyBufferIfNeeded(refreshes);

        return Observable.Create<IChangeSet<TObject, object>>(observer =>
        {
            var latest = new Dictionary<object, TObject>();
            var disp = new CompositeDisposable();

            // Maintain latest dictionary while forwarding original changes.
            _source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case Kernel.ChangeReason.Add:
                        case Kernel.ChangeReason.Update:
                            latest[change.Key] = change.Current;
                            break;
                        case Kernel.ChangeReason.Remove:
                            latest.Remove(change.Key);
                            break;
                        case Kernel.ChangeReason.Refresh:
                            // no structural update
                            break;
                    }
                }
                observer.OnNext(changes);
            }, observer.OnErrorResume, observer.OnCompleted).AddTo(disp);

            // Emit refresh changes.
            bufferedRefreshKeys.Subscribe(key =>
            {
                if (latest.TryGetValue(key, out var item))
                {
                    var cs = new ChangeSet<TObject, object>();
                    cs.Add(new Change<TObject, object>(Kernel.ChangeReason.Refresh, key, item));
                    observer.OnNext(cs);
                }
            }, observer.OnErrorResume, observer.OnCompleted).AddTo(disp);

            return disp;
        });
    }

    private Observable<object> ApplyBufferIfNeeded(Observable<object> source)
    {
        if (_buffer.HasValue)
        {
            var tp = _timeProvider ?? ObservableSystem.DefaultTimeProvider;
            return source.Debounce(_buffer.Value, tp);
        }
        return source;
    }
}
