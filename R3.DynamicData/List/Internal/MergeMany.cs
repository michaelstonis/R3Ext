// Port of DynamicData to R3.

using R3.DynamicData.List;

namespace R3.DynamicData.List.Internal;

internal sealed class MergeMany<TSource, TDestination>
    where TSource : notnull
{
    private readonly Observable<IChangeSet<TSource>> _source;
    private readonly Func<TSource, Observable<TDestination>> _selector;

    public MergeMany(Observable<IChangeSet<TSource>> source, Func<TSource, Observable<TDestination>> selector)
    {
        _source = source;
        _selector = selector;
    }

    public Observable<TDestination> Run()
    {
        return Observable.Create<TDestination, MergeManyState<TSource, TDestination>>(
            new MergeManyState<TSource, TDestination>(_source, _selector),
            static (observer, state) =>
            {
                var counter = new SubscriptionCounter();
                var gate = new object();
                var disposables = new CompositeDisposable();

                new SubscribeMany<TSource>(
                    state.Source.Concat(counter.DeferCleanup),
                    item =>
                    {
                        counter.Added();
                        return state.Selector(item)
                            .Synchronize(gate)
                            .Do(onNext: observer.OnNext, onDispose: counter.Finally)
                            .Subscribe();
                    })
                .Run()
                .Subscribe(
                    observer,
                    static (_, _) => { },
                    static (ex, obs) => obs.OnErrorResume(ex),
                    static (result, obs) =>
                    {
                        if (result.IsSuccess)
                        {
                            obs.OnCompleted();
                        }
                        else
                        {
                            obs.OnCompleted(result);
                        }
                    })
                .AddTo(disposables);

                counter.AddTo(disposables);
                return disposables;
            });
    }

    private sealed class SubscriptionCounter : IDisposable
    {
        private readonly Subject<IChangeSet<TSource>> _subject = new();
        private int _subscriptionCount = 1;

        public Observable<IChangeSet<TSource>> DeferCleanup => Observable.Defer(() =>
        {
            CheckCompleted();
            return _subject.AsObservable();
        });

        public void Added() => Interlocked.Increment(ref _subscriptionCount);

        public void Finally() => CheckCompleted();

        public void Dispose() => _subject.Dispose();

        private void CheckCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCount) == 0)
            {
                _subject.OnCompleted();
            }
        }
    }

    private readonly struct MergeManyState<TItem, TDest>
        where TItem : notnull
    {
        public readonly Observable<IChangeSet<TItem>> Source;
        public readonly Func<TItem, Observable<TDest>> Selector;

        public MergeManyState(Observable<IChangeSet<TItem>> source, Func<TItem, Observable<TDest>> selector)
        {
            Source = source;
            Selector = selector;
        }
    }
}
