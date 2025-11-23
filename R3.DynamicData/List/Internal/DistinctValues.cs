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
        return Observable.Create<IChangeSet<TValue>, DistinctValuesState<T, TValue>>(
            new DistinctValuesState<T, TValue>(_source, _selector, _comparer),
            static (observer, state) =>
            {
                var distinct = new ChangeAwareList<TValue>();
                var counts = new Dictionary<TValue, int>(state.Comparer);

                var disp = state.Source.Subscribe(
                    (observer, distinct, counts, state),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            foreach (var change in changes)
                            {
                                switch (change.Reason)
                                {
                                    case ListChangeReason.Add:
                                        Add(tuple.state.Selector(change.Item), tuple.distinct, tuple.counts);
                                        break;
                                    case ListChangeReason.AddRange:
                                        if (change.Range.Count > 0)
                                        {
                                            foreach (var item in change.Range)
                                            {
                                                Add(tuple.state.Selector(item), tuple.distinct, tuple.counts);
                                            }
                                        }
                                        else
                                        {
                                            Add(tuple.state.Selector(change.Item), tuple.distinct, tuple.counts);
                                        }

                                        break;
                                    case ListChangeReason.Remove:
                                        Remove(tuple.state.Selector(change.Item), tuple.distinct, tuple.counts);
                                        break;
                                    case ListChangeReason.Replace:
                                        if (change.PreviousItem != null)
                                        {
                                            var prev = tuple.state.Selector(change.PreviousItem);
                                            var cur = tuple.state.Selector(change.Item);
                                            if (!tuple.state.Comparer.Equals(prev, cur))
                                            {
                                                Remove(prev, tuple.distinct, tuple.counts);
                                                Add(cur, tuple.distinct, tuple.counts);
                                            }
                                        }
                                        else
                                        {
                                            Add(tuple.state.Selector(change.Item), tuple.distinct, tuple.counts);
                                        }

                                        break;
                                    case ListChangeReason.Moved:
                                        break;
                                    case ListChangeReason.Clear:
                                        if (tuple.distinct.Count > 0)
                                        {
                                            // Force removal of each distinct value regardless of duplicate counts.
                                            foreach (var v in tuple.distinct.ToList())
                                            {
                                                if (tuple.counts.ContainsKey(v))
                                                {
                                                    tuple.counts[v] = 1; // ensure removal emitted
                                                }

                                                Remove(v, tuple.distinct, tuple.counts);
                                            }

                                            tuple.counts.Clear();
                                        }

                                        break;
                                    case ListChangeReason.Refresh:
                                        break;
                                }
                            }

                            var output = tuple.distinct.CaptureChanges();
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

    private readonly struct DistinctValuesState<TItem, TVal>
        where TVal : notnull
    {
        public readonly Observable<IChangeSet<TItem>> Source;
        public readonly Func<TItem, TVal> Selector;
        public readonly IEqualityComparer<TVal> Comparer;

        public DistinctValuesState(Observable<IChangeSet<TItem>> source, Func<TItem, TVal> selector, IEqualityComparer<TVal> comparer)
        {
            Source = source;
            Selector = selector;
            Comparer = comparer;
        }
    }
}
