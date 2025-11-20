// Copyright (c) 2025 Michael Stonis. All rights reserved.
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
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var current = new List<T>();
            var disp = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        DisposeForChanges(changes, current);
                        if (changes.Count > 0)
                        {
                            observer.OnNext(changes);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return disp;
        });
    }

    private void DisposeItem(T item)
    {
        try
        {
            if (_disposeAction != null)
            {
                _disposeAction(item);
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

    private void DisposeRange(IEnumerable<T> items)
    {
        foreach (var i in items)
        {
            DisposeItem(i);
        }
    }

    private void DisposeForChanges(IChangeSet<T> changes, List<T> current)
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
                        // RemoveRange always contiguous starting at CurrentIndex
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
                    // Refresh does not imply disposal
                    break;
            }
        }
    }
}
