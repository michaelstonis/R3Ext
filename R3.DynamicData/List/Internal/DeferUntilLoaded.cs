using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class DeferUntilLoaded<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;

    public DeferUntilLoaded(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Defer(() =>
        {
            bool isLoaded = false;
            var subject = new Subject<IChangeSet<T>>();

            var subscription = _source.Subscribe(
                changes =>
                {
                    if (!isLoaded && changes.Count > 0)
                    {
                        isLoaded = true;
                    }

                    if (isLoaded)
                    {
                        subject.OnNext(changes);
                    }
                },
                subject.OnErrorResume, subject.OnCompleted);

            return subject.AsObservable().Do(onDispose: () => subscription.Dispose());
        });
    }
}
