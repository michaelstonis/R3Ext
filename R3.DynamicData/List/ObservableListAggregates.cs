// Port of DynamicData to R3.

namespace R3.DynamicData.List;

#pragma warning disable SA1116 // Parameter spans multiple lines
#pragma warning disable SA1513 // Blank line after closing brace
#pragma warning disable SA1503 // Braces should not be omitted

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

    // Returns the maximum value of the selected property. If the sequence is empty, returns default(TProperty).
    public static Observable<TProperty> Max<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return Observable.Create<TProperty>(observer =>
        {
            var values = new List<TProperty>();
            void Publish()
            {
                try
                {
                    var result = values.Count == 0 ? default : values.Max();
                    observer.OnNext(result);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            values.Add(selector(change.Item));
                            break;
                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                values.AddRange(change.Range.Select(selector));
                            }
                            else
                            {
                                values.Add(selector(change.Item));
                            }
                            break;
                        case ListChangeReason.Remove:
                            values.Remove(selector(change.Item));
                            break;
                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var v in change.Range.Select(selector))
                                {
                                    values.Remove(v);
                                }
                            }
                            else
                            {
                                values.Remove(selector(change.Item));
                            }
                            break;
                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                values.Remove(selector(change.PreviousItem));
                            }
                            values.Add(selector(change.Item));
                            break;
                        case ListChangeReason.Clear:
                            values.Clear();
                            break;
                        case ListChangeReason.Refresh:
                            // Refresh does not carry item payload in this port; recompute entire list not possible without underlying list.
                            break;
                    }
                }
                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // Returns the minimum value of the selected property. If the sequence is empty, returns default(TProperty).
    public static Observable<TProperty> Min<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return Observable.Create<TProperty>(observer =>
        {
            var values = new List<TProperty>();
            void Publish()
            {
                try
                {
                    var result = values.Count == 0 ? default : values.Min();
                    observer.OnNext(result);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            values.Add(selector(change.Item));
                            break;
                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                values.AddRange(change.Range.Select(selector));
                            }
                            else
                            {
                                values.Add(selector(change.Item));
                            }
                            break;
                        case ListChangeReason.Remove:
                            values.Remove(selector(change.Item));
                            break;
                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var v in change.Range.Select(selector))
                                {
                                    values.Remove(v);
                                }
                            }
                            else
                            {
                                values.Remove(selector(change.Item));
                            }
                            break;
                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                values.Remove(selector(change.PreviousItem));
                            }
                            values.Add(selector(change.Item));
                            break;
                        case ListChangeReason.Clear:
                            values.Clear();
                            break;
                        case ListChangeReason.Refresh:
                            break;
                    }
                }
                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // Returns the average (mean) of the selected numeric property. Empty sequence -> 0.
    public static Observable<double> Avg<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return Observable.Create<double>(observer =>
        {
            var values = new List<double>();
            void Publish()
            {
                try
                {
                    double result = values.Count == 0 ? 0.0 : values.Average();
                    observer.OnNext(result);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            values.Add(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                values.AddRange(change.Range.Select(x => Convert.ToDouble(selector(x))));
                            }
                            else
                            {
                                values.Add(Convert.ToDouble(selector(change.Item)));
                            }
                            break;
                        case ListChangeReason.Remove:
                            values.Remove(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var v in change.Range.Select(x => Convert.ToDouble(selector(x))))
                                {
                                    values.Remove(v);
                                }
                            }
                            else
                            {
                                values.Remove(Convert.ToDouble(selector(change.Item)));
                            }
                            break;
                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                values.Remove(Convert.ToDouble(selector(change.PreviousItem)));
                            }
                            values.Add(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.Clear:
                            values.Clear();
                            break;
                        case ListChangeReason.Refresh:
                            break;
                    }
                }
                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    // Returns the (population) standard deviation of the selected numeric property. Empty sequence -> 0.
    public static Observable<double> StdDev<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        return Observable.Create<double>(observer =>
        {
            var values = new List<double>();
            void Publish()
            {
                try
                {
                    if (values.Count == 0)
                    {
                        observer.OnNext(0.0);
                        return;
                    }

                    double mean = values.Average();
                    double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count; // population variance
                    observer.OnNext(Math.Sqrt(variance));
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            values.Add(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                values.AddRange(change.Range.Select(x => Convert.ToDouble(selector(x))));
                            }
                            else
                            {
                                values.Add(Convert.ToDouble(selector(change.Item)));
                            }
                            break;
                        case ListChangeReason.Remove:
                            values.Remove(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var v in change.Range.Select(x => Convert.ToDouble(selector(x))))
                                {
                                    values.Remove(v);
                                }
                            }
                            else
                            {
                                values.Remove(Convert.ToDouble(selector(change.Item)));
                            }
                            break;
                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                values.Remove(Convert.ToDouble(selector(change.PreviousItem)));
                            }
                            values.Add(Convert.ToDouble(selector(change.Item)));
                            break;
                        case ListChangeReason.Clear:
                            values.Clear();
                            break;
                        case ListChangeReason.Refresh:
                            break;
                    }
                }
                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }
}
