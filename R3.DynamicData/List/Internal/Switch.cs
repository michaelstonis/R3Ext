// Port of DynamicData to R3.

using System;
using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class Switch<T>(Observable<Observable<IChangeSet<T>>> sources)
    where T : notnull
{
    private readonly Observable<Observable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public Observable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
        observer =>
        {
            var locker = new object();

            var currentSubscription = new SerialDisposable();

            var outerSubscription = _sources.Subscribe(
                innerSource =>
                {
                    lock (locker)
                    {
                        currentSubscription.Disposable = innerSource.Subscribe(observer);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return Disposable.Create(() =>
            {
                outerSubscription.Dispose();
                currentSubscription.Dispose();
            });
        });
}
