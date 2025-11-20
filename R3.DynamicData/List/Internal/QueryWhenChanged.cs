using R3;

namespace R3.DynamicData.List.Internal;

internal sealed class QueryWhenChanged<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;

    public QueryWhenChanged(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IReadOnlyList<T>> Run()
    {
        return Observable.Create<IReadOnlyList<T>>(observer =>
        {
            var list = new List<T>();

            return _source.Subscribe(
                changes =>
                {
                    // Apply changes to the list
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
                                break;

                            case ListChangeReason.RemoveRange:
                                for (int i = 0; i < change.Range.Count; i++)
                                {
                                    list.RemoveAt(change.CurrentIndex);
                                }

                                break;

                            case ListChangeReason.Replace:
                                list[change.CurrentIndex] = change.Item;
                                break;

                            case ListChangeReason.Moved:
                                var movedItem = list[change.PreviousIndex];
                                list.RemoveAt(change.PreviousIndex);
                                list.Insert(change.CurrentIndex, movedItem);
                                break;

                            case ListChangeReason.Clear:
                                list.Clear();
                                break;
                        }
                    }

                    // Emit the current state as a read-only list
                    observer.OnNext(list.AsReadOnly());
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }
}
