// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class Reverse<T>
{
    private readonly Observable<IChangeSet<T>> _source;

    public Reverse(Observable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public Observable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            var list = new List<T>();

            var disposable = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        // Apply changes to maintain state
                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    list.Insert(change.CurrentIndex, change.Item);
                                    break;

                                case ListChangeReason.AddRange:
                                    if (change.Range.Count > 0)
                                    {
                                        int idx = change.CurrentIndex;
                                        foreach (var rangeItem in change.Range)
                                        {
                                            list.Insert(idx++, rangeItem);
                                        }
                                    }
                                    else
                                    {
                                        list.Insert(change.CurrentIndex, change.Item);
                                    }

                                    break;

                                case ListChangeReason.Remove:
                                    list.RemoveAt(change.CurrentIndex);
                                    break;

                                case ListChangeReason.RemoveRange:
                                    if (change.Range.Count > 0)
                                    {
                                        list.RemoveRange(change.CurrentIndex, change.Range.Count);
                                    }
                                    else
                                    {
                                        list.RemoveAt(change.CurrentIndex);
                                    }

                                    break;

                                case ListChangeReason.Replace:
                                    list[change.CurrentIndex] = change.Item;
                                    break;

                                case ListChangeReason.Moved:
                                    var item = list[change.PreviousIndex];
                                    list.RemoveAt(change.PreviousIndex);
                                    list.Insert(change.CurrentIndex, item);
                                    break;

                                case ListChangeReason.Clear:
                                    list.Clear();
                                    break;

                                case ListChangeReason.Refresh:
                                    // No state change needed for refresh
                                    break;
                            }
                        }

                        // Emit the reversed list as Clear + AddRange
                        var changeSet = new ChangeSet<T>(2);
                        if (list.Count > 0)
                        {
                            changeSet.Add(new Change<T>(ListChangeReason.Clear, Array.Empty<T>(), 0));
                            var reversed = list.AsEnumerable().Reverse().ToList();
                            changeSet.Add(new Change<T>(ListChangeReason.AddRange, reversed, 0));
                        }
                        else
                        {
                            changeSet.Add(new Change<T>(ListChangeReason.Clear, Array.Empty<T>(), 0));
                        }

                        if (changeSet.Count > 0)
                        {
                            observer.OnNext(changeSet);
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
}
