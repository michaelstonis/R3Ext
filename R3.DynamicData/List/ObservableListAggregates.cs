// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

public static class ObservableListAggregates
{
    public static Observable<int> Count<T>(this Observable<IChangeSet<T>> source)
    {
        return Observable.Create<int>(observer =>
        {
            int count = 0;
            return source.Subscribe(
                changes =>
                {
                    try
                    {
                        foreach (var c in changes)
                        {
                            switch (c.Reason)
                            {
                                case ListChangeReason.Add:
                                    count += 1;
                                    break;
                                case ListChangeReason.AddRange:
                                    count += c.Range.Count > 0 ? c.Range.Count : 1;
                                    break;
                                case ListChangeReason.Remove:
                                    count -= 1;
                                    break;
                                case ListChangeReason.RemoveRange:
                                    count -= c.Range.Count > 0 ? c.Range.Count : 1;
                                    break;
                                case ListChangeReason.Clear:
                                    count = 0;
                                    break;
                            }
                        }

                        observer.OnNext(count);
                    }
                    catch (Exception ex)
                    {
                        observer.OnErrorResume(ex);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    public static Observable<int> Sum(this Observable<IChangeSet<int>> source)
    {
        return source.Scan(0, (sum, changes) =>
        {
            int s = sum;
            foreach (var c in changes)
            {
                switch (c.Reason)
                {
                    case ListChangeReason.Add:
                        s += c.Item;
                        break;
                    case ListChangeReason.AddRange:
                        if (c.Range.Count > 0)
                        {
                            s += c.Range.Sum();
                        }
                        else
                        {
                            s += c.Item;
                        }

                        break;
                    case ListChangeReason.Remove:
                        s -= c.Item;
                        break;
                    case ListChangeReason.RemoveRange:
                        if (c.Range.Count > 0)
                        {
                            s -= c.Range.Sum();
                        }
                        else
                        {
                            s -= c.Item;
                        }

                        break;
                    case ListChangeReason.Replace:
                        if (c.PreviousItem != null)
                        {
                            s -= c.PreviousItem;
                        }

                        s += c.Item;
                        break;
                    case ListChangeReason.Clear:
                        s = 0;
                        break;
                }
            }

            return s;
        });
    }
}
