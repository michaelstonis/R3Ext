// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class Transform<TSource, TResult>
{
    private readonly Observable<IChangeSet<TSource>> _source;
    private readonly Func<TSource, TResult> _selector;

    public Transform(Observable<IChangeSet<TSource>> source, Func<TSource, TResult> selector)
    {
        _source = source;
        _selector = selector;
    }

    public Observable<IChangeSet<TResult>> Run()
    {
        return Observable.Create<IChangeSet<TResult>>(observer =>
        {
            var list = new ChangeAwareList<TResult>();
            var disposable = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        Process(list, changes);
                        var output = list.CaptureChanges();
                        if (output.Count > 0)
                        {
                            observer.OnNext(output);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);

            return disposable;
        });
    }

    private void Process(ChangeAwareList<TResult> target, IChangeSet<TSource> changes)
    {
        // Optimize for initial adds when empty
        if (target.Count == 0)
        {
            bool onlyAdds = true;
            foreach (var c in changes)
            {
                if (c.Reason != ListChangeReason.Add && c.Reason != ListChangeReason.AddRange)
                {
                    onlyAdds = false;
                    break;
                }
            }

            if (onlyAdds)
            {
                foreach (var c in changes)
                {
                    if (c.Reason == ListChangeReason.Add)
                    {
                        target.Add(_selector(c.Item));
                    }
                    else if (c.Reason == ListChangeReason.AddRange)
                    {
                        if (c.Range.Count > 0)
                        {
                            var projected = c.Range.Select(_selector).ToList();
                            foreach (var p in projected)
                            {
                                target.Add(p);
                            }
                        }
                        else
                        {
                            target.Add(_selector(c.Item));
                        }
                    }
                }

                return;
            }
        }

        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    target.Insert(change.CurrentIndex, _selector(change.Item));
                    break;
                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        var items = change.Range.Select(_selector).ToList();
                        target.InsertRange(items, change.CurrentIndex);
                    }
                    else
                    {
                        target.Insert(change.CurrentIndex, _selector(change.Item));
                    }

                    break;
                case ListChangeReason.Remove:
                    target.RemoveAt(change.CurrentIndex);
                    break;
                case ListChangeReason.RemoveRange:
                    if (change.Range.Count > 0)
                    {
                        target.RemoveRange(change.CurrentIndex, change.Range.Count);
                    }
                    else
                    {
                        target.RemoveAt(change.CurrentIndex);
                    }

                    break;
                case ListChangeReason.Replace:
                    target[change.CurrentIndex] = _selector(change.Item);
                    break;
                case ListChangeReason.Moved:
                    target.Move(change.PreviousIndex, change.CurrentIndex);
                    break;
                case ListChangeReason.Clear:
                    target.Clear();
                    break;
                case ListChangeReason.Refresh:
                    // map the item again; emit replace to reflect potential value changes
                    if (change.CurrentIndex >= 0 && change.CurrentIndex < target.Count)
                    {
                        target[change.CurrentIndex] = _selector(change.Item);
                    }

                    break;
            }
        }
    }
}
