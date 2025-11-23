// Port of DynamicData SuppressRefresh to R3.
#pragma warning disable SA1513, SA1503, SA1116
// Style suppression for internal operator implementation.
#pragma warning disable SA1116, SA1513, SA1516, SA1503, SA1127, SA1210
namespace R3.DynamicData.Cache.Internal;

internal sealed class SuppressRefresh<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    public SuppressRefresh(Observable<IChangeSet<TObject, TKey>> source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        var state = new SuppressRefreshState(_source);
        return Observable.Create<IChangeSet<TObject, TKey>, SuppressRefreshState>(
            state,
            static (observer, state) =>
        {
            return state.Source.Subscribe(changes =>
        {
            if (changes.Count == 0) return;
            var filtered = new ChangeSet<TObject, TKey>();
            foreach (var c in changes)
            {
                if (c.Reason == Kernel.ChangeReason.Refresh) continue;
                filtered.Add(c);
            }
            if (filtered.Count > 0) observer.OnNext(filtered);
        }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    private readonly struct SuppressRefreshState
    {
        public readonly Observable<IChangeSet<TObject, TKey>> Source;
        public SuppressRefreshState(Observable<IChangeSet<TObject, TKey>> source) => Source = source;
    }
}
