// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class AsyncDisposeMany<T>
    where T : notnull, IAsyncDisposable
{
    private readonly Observable<IChangeSet<T>> _source;

    public AsyncDisposeMany(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>, Observable<IChangeSet<T>>>(
            _source,
            static (observer, source) =>
            {
                var current = new List<T>();
                var disp = source.Subscribe(
                    (observer, current),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            DisposeForChanges(changes, tuple.current);
                            if (changes.Count > 0)
                            {
                                tuple.observer.OnNext(changes);
                            }
                        }
                        catch (Exception ex)
                        {
                            tuple.observer.OnErrorResume(ex);
                        }
                    },
                    static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                    static (result, tuple) =>
                    {
                        if (result.IsSuccess)
                        {
                            tuple.observer.OnCompleted();
                        }
                        else
                        {
                            tuple.observer.OnCompleted(result);
                        }
                    });

                return Disposable.Combine(
                    disp,
                    Disposable.Create(current, static current =>
                    {
                        DisposeRange(current);
                        current.Clear();
                    }));
            });
    }

    private static void DisposeItem(T item)
    {
        try
        {
            _ = item.DisposeAsync().AsTask();
        }
        catch
        {
            // Swallow disposal exceptions to avoid breaking stream; pattern aligns with DynamicData behavior.
        }
    }

    private static void DisposeRange(IEnumerable<T> items)
    {
        foreach (var i in items)
        {
            DisposeItem(i);
        }
    }

    private static void DisposeForChanges(IChangeSet<T> changes, List<T> current)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    current.Insert(change.CurrentIndex, change.Item);
                    break;

                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        int idx = change.CurrentIndex;
                        foreach (var item in change.Range)
                        {
                            current.Insert(idx++, item);
                        }
                    }
                    else
                    {
                        current.Insert(change.CurrentIndex, change.Item);
                    }

                    break;

                case ListChangeReason.Remove:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < current.Count)
                    {
                        var removed = current[change.CurrentIndex];
                        current.RemoveAt(change.CurrentIndex);
                        DisposeItem(removed);
                    }

                    break;

                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        var toRemove = current.Skip(change.CurrentIndex).Take(change.Range.Count).ToList();
                        current.RemoveRange(change.CurrentIndex, change.Range.Count);
                        DisposeRange(toRemove);
                    }
                    else
                    {
                        if (change.CurrentIndex >= 0 && change.CurrentIndex < current.Count)
                        {
                            var removed = current[change.CurrentIndex];
                            current.RemoveAt(change.CurrentIndex);
                            DisposeItem(removed);
                        }
                    }

                    break;

                case ListChangeReason.Replace:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < current.Count && change.PreviousItem != null)
                    {
                        var old = current[change.CurrentIndex];
                        current[change.CurrentIndex] = change.Item;
                        DisposeItem(old);
                    }

                    break;

                case ListChangeReason.Moved:
                    if (change.PreviousIndex >= 0 && change.PreviousIndex < current.Count)
                    {
                        var item = current[change.PreviousIndex];
                        current.RemoveAt(change.PreviousIndex);
                        current.Insert(change.CurrentIndex, item);
                    }

                    break;

                case ListChangeReason.Clear:
                    DisposeRange(current);
                    current.Clear();
                    break;

                case ListChangeReason.Refresh:
                    // Refresh does not imply disposal.
                    break;
            }
        }
    }
}
