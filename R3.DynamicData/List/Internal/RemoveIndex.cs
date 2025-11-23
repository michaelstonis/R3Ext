// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class RemoveIndex<T>
{
    private readonly Observable<IChangeSet<T>> _source;

    public RemoveIndex(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>, Observable<IChangeSet<T>>>(
            _source,
            static (observer, source) =>
            {
                var disposable = source.Subscribe(
                    observer,
                    static (changes, obs) =>
                    {
                        try
                        {
                            var changeSet = new ChangeSet<T>(changes.Count);

                            foreach (var change in changes)
                            {
                                var newChange = change.Reason switch
                                {
                                    ListChangeReason.Add => new Change<T>(
                                        ListChangeReason.Add,
                                        change.Item,
                                        -1),

                                    ListChangeReason.AddRange => new Change<T>(
                                        ListChangeReason.AddRange,
                                        change.Range,
                                        -1),

                                    ListChangeReason.Remove => new Change<T>(
                                        ListChangeReason.Remove,
                                        change.Item,
                                        -1),

                                    ListChangeReason.RemoveRange => new Change<T>(
                                        ListChangeReason.RemoveRange,
                                        change.Range,
                                        -1),

                                    ListChangeReason.Replace => new Change<T>(
                                        ListChangeReason.Replace,
                                        change.Item,
                                        change.PreviousItem,
                                        -1),

                                    ListChangeReason.Moved => new Change<T>(
                                        ListChangeReason.Moved,
                                        change.Item,
                                        -1,
                                        -1),

                                    ListChangeReason.Refresh => new Change<T>(
                                        ListChangeReason.Refresh,
                                        change.Item,
                                        -1),

                                    ListChangeReason.Clear => new Change<T>(
                                        ListChangeReason.Clear,
                                        Array.Empty<T>(),
                                        -1),

                                    _ => change,
                                };

                                changeSet.Add(newChange);
                            }

                            if (changeSet.Count > 0)
                            {
                                obs.OnNext(changeSet);
                            }
                        }
                        catch (Exception ex)
                        {
                            obs.OnErrorResume(ex);
                        }
                    },
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
                    });

                return disposable;
            });
    }
}
