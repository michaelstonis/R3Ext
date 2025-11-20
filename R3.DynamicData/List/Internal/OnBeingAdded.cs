using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class OnBeingAdded<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Action<T> _addAction;

    public OnBeingAdded(Observable<IChangeSet<T>> source, Action<T> addAction)
    {
        _source = source;
        _addAction = addAction;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return _source.Do(changes =>
        {
            foreach (var change in changes)
            {
                if (change.Reason == ListChangeReason.Add || change.Reason == ListChangeReason.AddRange)
                {
                    if (change.Range.Count > 0)
                    {
                        foreach (var item in change.Range)
                        {
                            _addAction(item);
                        }
                    }
                    else
                    {
                        _addAction(change.Item);
                    }
                }
            }
        });
    }
}
