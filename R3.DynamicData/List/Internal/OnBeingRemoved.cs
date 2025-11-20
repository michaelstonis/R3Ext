using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class OnBeingRemoved<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Action<T> _removeAction;
    private readonly bool _invokeOnUnsubscribe;

    public OnBeingRemoved(Observable<IChangeSet<T>> source, Action<T> removeAction, bool invokeOnUnsubscribe)
    {
        _source = source;
        _removeAction = removeAction;
        _invokeOnUnsubscribe = invokeOnUnsubscribe;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var list = new List<T>();

            var subscription = _source.Subscribe(
                changes =>
                {
                    // Track all items
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                list.Insert(change.CurrentIndex, change.Item);
                                break;

                            case ListChangeReason.AddRange:
                                list.InsertRange(change.CurrentIndex, change.Range);
                                break;

                            case ListChangeReason.Remove:
                                list.RemoveAt(change.CurrentIndex);
                                _removeAction(change.Item);
                                break;

                            case ListChangeReason.RemoveRange:
                                for (int i = 0; i < change.Range.Count; i++)
                                {
                                    var item = list[change.CurrentIndex];
                                    list.RemoveAt(change.CurrentIndex);
                                    _removeAction(item);
                                }

                                break;

                            case ListChangeReason.Replace:
                                if (change.PreviousItem != null)
                                {
                                    _removeAction(change.PreviousItem);
                                }

                                break;

                            case ListChangeReason.Moved:
                                var movedItem = list[change.PreviousIndex];
                                list.RemoveAt(change.PreviousIndex);
                                list.Insert(change.CurrentIndex, movedItem);
                                break;

                            case ListChangeReason.Clear:
                                foreach (var item in list)
                                {
                                    _removeAction(item);
                                }

                                list.Clear();
                                break;
                        }
                    }

                    observer.OnNext(changes);
                },
                observer.OnErrorResume, observer.OnCompleted);

            return R3.Disposable.Create(
                () =>
                {
                    subscription.Dispose();

                    if (_invokeOnUnsubscribe)
                    {
                        foreach (var item in list)
                        {
                            _removeAction(item);
                        }
                    }
                });
        });
    }
}
