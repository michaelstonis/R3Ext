// Port of DynamicData to R3.
// Audited against DD #968 (Switch error propagation fix).
// Each inner source is subscribed with a fresh forwarding observer (lambdas) rather than by
// reusing the single downstream observer instance: R3 observers are single-subscription, so
// reusing the same observer across successive inner sources throws on the second subscribe.
// OnNext and OnErrorResume are forwarded downstream; inner OnCompleted is intentionally ignored
// so the merged stream only completes when the outer source of sources completes.

using System;
using R3;

namespace R3Ext.DynamicData.List.Internal;

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
                        currentSubscription.Disposable = innerSource.Subscribe(
                            value => observer.OnNext(value),
                            error => observer.OnErrorResume(error),
                            static _ => { });
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
