// Copyright (c) 2025 Michael Stonis. All rights reserved.
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
            var shared = _source.Publish();
            var disposables = new CompositeDisposable();

            // Subscribe to the transformed IDisposable stream to manage subscriptions
            shared
                .Transform(_subscriptionFactory)
                .DisposeMany(d => d.Dispose())
                .Subscribe(_ => { }, observer.OnErrorResume, _ => { })
                .AddTo(disposables);

            // Forward the original changeset to the observer
            shared.Subscribe(observer).AddTo(disposables);

            // Connect to activate the shared source
            shared.Connect().AddTo(disposables);

            return disposables;
        });
    }
}
