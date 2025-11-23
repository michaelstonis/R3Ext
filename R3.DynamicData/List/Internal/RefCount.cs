using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class RefCount<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;

    public RefCount(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return _source.Publish().RefCount();
    }
}
