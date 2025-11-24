// Port of DynamicData IncludeUpdateWhen to R3.

namespace R3.DynamicData.Cache.Internal;

internal sealed class IncludeUpdateWhen<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Observable<IChangeSet<TObject, TKey>> _source;
    private readonly Func<TObject, TObject?, bool> _predicate;

    public IncludeUpdateWhen(Observable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject?, bool> predicate)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public Observable<IChangeSet<TObject, TKey>> Run()
    {
        var state = new IncludeUpdateWhenState(_source, _predicate);
        return Observable.Create<IChangeSet<TObject, TKey>, IncludeUpdateWhenState>(
            state,
            static (observer, state) =>
        {
            return state.Source.Subscribe(
                changes =>
        {
            if (changes.Count == 0)
            {
                return;
            }

            var filtered = new ChangeSet<TObject, TKey>();
            foreach (var c in changes)
            {
                if (c.Reason == Kernel.ChangeReason.Update)
                {
                    var prev = c.Previous.HasValue ? c.Previous.Value : default;
                    if (!state.Predicate(c.Current, prev))
                    {
                        continue; // suppress this update
                    }
                }

                filtered.Add(c);
            }

            if (filtered.Count > 0)
            {
                observer.OnNext(filtered);
            }
        },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    private readonly struct IncludeUpdateWhenState
    {
        public readonly Observable<IChangeSet<TObject, TKey>> Source;
        public readonly Func<TObject, TObject?, bool> Predicate;

        public IncludeUpdateWhenState(Observable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject?, bool> predicate)
        {
            Source = source;
            Predicate = predicate;
        }
    }
}
