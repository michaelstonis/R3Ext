// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List.Internal;

internal sealed class DistinctValues<T, TValue>
    where TValue : notnull
{
    private readonly Observable<IChangeSet<T>> _source;
    private readonly Func<T, TValue> _selector;
    private readonly IEqualityComparer<TValue> _comparer;

    public DistinctValues(Observable<IChangeSet<T>> source, Func<T, TValue> selector, IEqualityComparer<TValue>? comparer = null)
    {
        _source = source;
        _selector = selector;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;
    }

    public Observable<IChangeSet<TValue>> Run()
    {
        return Observable.Create<IChangeSet<TValue>>(observer =>
        {
            var distinct = new ChangeAwareList<TValue>();
            var counts = new Dictionary<TValue, int>(_comparer);

            var disp = _source.Subscribe(
                changes =>
                {
                    try
                    {
                        foreach (var change in changes)
                        {
                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    Add(_selector(change.Item), distinct, counts);
                                    break;
                                case ListChangeReason.AddRange:
                                    if (change.Range.Count > 0)
                                    {
                                        foreach (var item in change.Range)
                                        {
                                            Add(_selector(item), distinct, counts);
                                        }
                                    }
                                    else
                                    {
                                        Add(_selector(change.Item), distinct, counts);
                                    }

                                    break;
                                case ListChangeReason.Remove:
                                    Remove(_selector(change.Item), distinct, counts);
                                    break;
                                case ListChangeReason.Replace:
                                    if (change.PreviousItem != null)
                                    {
                                        var prev = _selector(change.PreviousItem);
                                        var cur = _selector(change.Item);
                                        if (!_comparer.Equals(prev, cur))
                                        {
                                            Remove(prev, distinct, counts);
                                            Add(cur, distinct, counts);
                                        }
                                    }
                                    else
                                    {
                                        Add(_selector(change.Item), distinct, counts);
                                    }

                                    break;
                                case ListChangeReason.Moved:
                                    break;
                                case ListChangeReason.Clear:
                                    if (distinct.Count > 0)
                                    {
                                        // Force removal of each distinct value regardless of duplicate counts.
                                        foreach (var v in distinct.ToList())
                                        {
                                            if (counts.ContainsKey(v))
                                            {
                                                counts[v] = 1; // ensure removal emitted
                                            }

                                            Remove(v, distinct, counts);
                                        }

                                        counts.Clear();
                                    }

                                    break;
                                case ListChangeReason.Refresh:
                                    break;
                            }
                        }

                        var output = distinct.CaptureChanges();
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

            return disp;
        });
    }

    private static void Add(TValue value, ChangeAwareList<TValue> distinct, Dictionary<TValue, int> counts)
    {
        if (counts.TryGetValue(value, out var c))
        {
            counts[value] = c + 1;
            return;
        }

        counts[value] = 1;
        distinct.Add(value);
    }

    private static void Remove(TValue value, ChangeAwareList<TValue> distinct, Dictionary<TValue, int> counts)
    {
        if (!counts.TryGetValue(value, out var c))
        {
            return;
        }

        if (c > 1)
        {
            counts[value] = c - 1;
            return;
        }

        counts.Remove(value);
        var idx = distinct.IndexOf(value);
        if (idx >= 0)
        {
            distinct.RemoveAt(idx);
        }
    }
}
