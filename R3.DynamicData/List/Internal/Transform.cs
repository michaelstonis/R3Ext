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
        return Observable.Create<IChangeSet<TResult>, TransformState<TSource, TResult>>(
            new TransformState<TSource, TResult>(_source, _selector),
            static (observer, state) =>
            {
                var list = new ChangeAwareList<TResult>();
                var disposable = state.Source.Subscribe(
                    (observer, state, list),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            Process(tuple.list, changes, tuple.state.Selector);
                            var output = tuple.list.CaptureChanges();
                            if (output.Count > 0)
                            {
                                tuple.observer.OnNext(output);
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

                return disposable;
            });
    }

    private static void Process(ChangeAwareList<TResult> target, IChangeSet<TSource> changes, Func<TSource, TResult> selector)
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
                        target.Add(selector(c.Item));
                    }
                    else if (c.Reason == ListChangeReason.AddRange)
                    {
                        if (c.Range.Count > 0)
                        {
                            var projected = c.Range.Select(selector).ToList();
                            foreach (var p in projected)
                            {
                                target.Add(p);
                            }
                        }
                        else
                        {
                            target.Add(selector(c.Item));
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
                    target.Insert(change.CurrentIndex, selector(change.Item));
                    break;
                case ListChangeReason.AddRange:
                    if (change.Range.Count > 0)
                    {
                        var items = change.Range.Select(selector).ToList();
                        target.InsertRange(items, change.CurrentIndex);
                    }
                    else
                    {
                        target.Insert(change.CurrentIndex, selector(change.Item));
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
                    target[change.CurrentIndex] = selector(change.Item);
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
                        target[change.CurrentIndex] = selector(change.Item);
                    }

                    break;
            }
        }
    }

    private readonly struct TransformState<TSourceItem, TResultItem>
    {
        public readonly Observable<IChangeSet<TSourceItem>> Source;
        public readonly Func<TSourceItem, TResultItem> Selector;

        public TransformState(Observable<IChangeSet<TSourceItem>> source, Func<TSourceItem, TResultItem> selector)
        {
            Source = source;
            Selector = selector;
        }
    }
}
