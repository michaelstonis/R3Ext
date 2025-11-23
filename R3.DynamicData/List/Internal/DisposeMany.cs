// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class DisposeMany<T>
    where T : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Action<T>? _disposeAction;

    public DisposeMany(Observable<IChangeSet<T>> source, Action<T>? disposeAction = null)
    {
        _source = source;
        _disposeAction = disposeAction;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>, DisposeManyState<T>>(
            new DisposeManyState<T>(_source, _disposeAction),
            static (observer, state) =>
            {
                var current = new List<T>();
                var disp = state.Source.Subscribe(
                    (observer, state, current),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            DisposeForChanges(changes, tuple.current, tuple.state.DisposeAction);
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

                return Disposable.Create((current, state), static tuple =>
                {
                    // On unsubscribe, dispose any remaining items to mirror cache DisposeMany semantics.
                    DisposeRange(tuple.current, tuple.state.DisposeAction);
                    tuple.current.Clear();
                });
            });
    }

    private static void DisposeItem(T item, Action<T>? disposeAction)
    {
        try
        {
            if (disposeAction != null)
            {
                disposeAction(item);
                return;
            }

            if (item is IDisposable d)
            {
                d.Dispose();
            }
        }
        catch
        {
            // Swallow disposal exceptions to avoid breaking stream; pattern aligns with DynamicData behavior.
        }
    }

    private static void DisposeRange(IEnumerable<T> items, Action<T>? disposeAction)
    {
        foreach (var i in items)
        {
            DisposeItem(i, disposeAction);
        }
    }

    private static void DisposeForChanges(IChangeSet<T> changes, List<T> current, Action<T>? disposeAction)
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
                        DisposeItem(removed, disposeAction);
                    }

                    break;

                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        // RemoveRange always contiguous starting at CurrentIndex
                        var toRemove = current.Skip(change.CurrentIndex).Take(change.Range.Count).ToList();
                        current.RemoveRange(change.CurrentIndex, change.Range.Count);
                        DisposeRange(toRemove, disposeAction);
                    }
                    else
                    {
                        if (change.CurrentIndex >= 0 && change.CurrentIndex < current.Count)
                        {
                            var removed = current[change.CurrentIndex];
                            current.RemoveAt(change.CurrentIndex);
                            DisposeItem(removed, disposeAction);
                        }
                    }

                    break;

                case ListChangeReason.Replace:
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < current.Count && change.PreviousItem != null)
                    {
                        var old = current[change.CurrentIndex];
                        current[change.CurrentIndex] = change.Item;
                        DisposeItem(old, disposeAction);
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
                    DisposeRange(current, disposeAction);
                    current.Clear();
                    break;

                case ListChangeReason.Refresh:
                    // Refresh does not imply disposal
                    break;
            }
        }
    }

    private readonly struct DisposeManyState<TItem>
        where TItem : notnull
    {
        public readonly Observable<IChangeSet<TItem>> Source;
        public readonly Action<TItem>? DisposeAction;

        public DisposeManyState(Observable<IChangeSet<TItem>> source, Action<TItem>? disposeAction)
        {
            Source = source;
            DisposeAction = disposeAction;
        }
    }
}
