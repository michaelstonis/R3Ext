// Port of DynamicData to R3.

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
        return Observable.Create<TDestination>(observer =>
        {
            var counter = new SubscriptionCounter();
            var gate = new object();
            var disposables = new CompositeDisposable();

            _source
                .Concat(counter.DeferCleanup)
                .SubscribeMany(item =>
                {
                    counter.Added();
                    return _selector(item)
                        .Synchronize(gate)
                        .Do(onNext: observer.OnNext, onDispose: counter.Finally)
                        .Subscribe();
                })
                .Subscribe(
                    static _ => { },
                    observer.OnErrorResume,
                    observer.OnCompleted)
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
}
