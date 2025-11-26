// Port of DynamicData to R3.

using R3.DynamicData.Utilities;

namespace R3.DynamicData.List;

/// <summary>
/// Aggregate operators for observable lists.
/// </summary>
public static class ObservableListAggregates
{
    /// <summary>
    /// Tracks the count of items in the list, reactively updating as items are added or removed.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <returns>An observable that emits the current count.</returns>
    public static Observable<int> Count<T>(this Observable<IChangeSet<T>> source)
    {
        return Observable.Create<int, Observable<IChangeSet<T>>>(
            source,
            static (observer, state) =>
            {
                var count = new RefInt();
                return state.Subscribe(
                    (observer, count),
                    static (changes, tuple) =>
                    {
                        try
                        {
                            foreach (var c in changes)
                            {
                                switch (c.Reason)
                                {
                                    case ListChangeReason.Add:
                                        tuple.count.Increment();
                                        break;
                                    case ListChangeReason.AddRange:
                                        tuple.count.Add(c.Range.Count > 0 ? c.Range.Count : 1);
                                        break;
                                    case ListChangeReason.Remove:
                                        tuple.count.Decrement();
                                        break;
                                    case ListChangeReason.RemoveRange:
                                        tuple.count.Subtract(c.Range.Count > 0 ? c.Range.Count : 1);
                                        break;
                                    case ListChangeReason.Clear:
                                        tuple.count.Value = 0;
                                        break;
                                }
                            }

                            tuple.observer.OnNext(tuple.count.Value);
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
            });
    }

    /// <summary>
    /// Calculates the sum of integer items in the list, reactively updating as items change.
    /// </summary>
    /// <param name="source">The source observable list of integers.</param>
    /// <returns>An observable that emits the current sum.</returns>
    public static Observable<int> Sum(this Observable<IChangeSet<int>> source)
    {
        return source.Scan(0, static (sum, changes) =>
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
                        s -= c.PreviousItem;
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

    /// <summary>
    /// Finds the maximum value of a property across all items in the list, reactively updating as items change.
    /// </summary>
    /// <typeparam name="TSource">The type of items in the list.</typeparam>
    /// <typeparam name="TProperty">The type of the property to compare (must be comparable).</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="selector">Function to select the property value from each item.</param>
    /// <returns>An observable that emits the current maximum value, or default if empty.</returns>
    public static Observable<TProperty> Max<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<TProperty>(observer =>
        {
            // Track per-item value to support Refresh.
            var itemValues = new Dictionary<TSource, TProperty>();
            var valueCounts = new Dictionary<TProperty, int>();
            bool hasValue = false;
            TProperty currentMax = default;

            void Increment(TSource item)
            {
                var value = selector(item);
                itemValues[item] = value;
                if (valueCounts.TryGetValue(value, out var count))
                {
                    valueCounts[value] = count + 1;
                }
                else
                {
                    valueCounts[value] = 1;
                }

                if (!hasValue || value.CompareTo(currentMax) > 0)
                {
                    currentMax = value;
                    hasValue = true;
                }
            }

            void Decrement(TSource item, TProperty value)
            {
                if (!valueCounts.TryGetValue(value, out var count))
                {
                    return;
                }

                if (count == 1)
                {
                    valueCounts.Remove(value);

                    // If we removed the current max, recalc.
                    if (hasValue && value.CompareTo(currentMax) == 0)
                    {
                        RecalculateMax();
                    }
                }
                else
                {
                    valueCounts[value] = count - 1;
                }

                itemValues.Remove(item);
                if (valueCounts.Count == 0)
                {
                    hasValue = false;
                    currentMax = default;
                }
            }

            void RecalculateMax()
            {
                if (valueCounts.Count == 0)
                {
                    hasValue = false;
                    currentMax = default;
                    return;
                }

                // Linear scan of distinct values.
                var max = default(TProperty);
                bool first = true;
                foreach (var kvp in valueCounts.Keys)
                {
                    if (first)
                    {
                        max = kvp;
                        first = false;
                    }
                    else if (kvp.CompareTo(max) > 0)
                    {
                        max = kvp;
                    }
                }

                currentMax = max;
                hasValue = true;
            }

            void Publish()
            {
                try
                {
                    observer.OnNext(hasValue ? currentMax : default);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(
                changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            Increment(change.Item);
                            break;
                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    Increment(i);
                                }
                            }
                            else
                            {
                                Increment(change.Item);
                            }

                            break;

                        case ListChangeReason.Remove:
                            if (itemValues.TryGetValue(change.Item, out var vRemove))
                            {
                                Decrement(change.Item, vRemove);
                            }

                            break;

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    if (itemValues.TryGetValue(i, out var vR))
                                    {
                                        Decrement(i, vR);
                                    }
                                }
                            }
                            else if (itemValues.TryGetValue(change.Item, out var vR2))
                            {
                                Decrement(change.Item, vR2);
                            }

                            break;

                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null && itemValues.TryGetValue(change.PreviousItem, out var vPrev))
                            {
                                Decrement(change.PreviousItem, vPrev);
                            }

                            Increment(change.Item);

                            break;

                        case ListChangeReason.Refresh:
                            // Refresh generated by AutoRefresh does not carry an item.
                            // Re-evaluate all items to reflect potential value changes.
                            if (itemValues.Count > 0)
                            {
                                valueCounts.Clear();
                                hasValue = false;
                                currentMax = default;
                                var keys = itemValues.Keys.ToList();
                                foreach (var it in keys)
                                {
                                    var newVal2 = selector(it);
                                    itemValues[it] = newVal2;
                                    if (valueCounts.TryGetValue(newVal2, out var c))
                                    {
                                        valueCounts[newVal2] = c + 1;
                                    }
                                    else
                                    {
                                        valueCounts[newVal2] = 1;
                                    }

                                    if (!hasValue || newVal2.CompareTo(currentMax) > 0)
                                    {
                                        currentMax = newVal2;
                                        hasValue = true;
                                    }
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            itemValues.Clear();
                            valueCounts.Clear();
                            hasValue = false;
                            currentMax = default;

                            break;
                    }
                }

                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    /// <summary>
    /// Finds the minimum value of a property across all items in the list, reactively updating as items change.
    /// </summary>
    /// <typeparam name="TSource">The type of items in the list.</typeparam>
    /// <typeparam name="TProperty">The type of the property to compare (must be comparable).</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="selector">Function to select the property value from each item.</param>
    /// <returns>An observable that emits the current minimum value, or default if empty.</returns>
    public static Observable<TProperty> Min<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<TProperty>(observer =>
        {
            var itemValues = new Dictionary<TSource, TProperty>();
            var valueCounts = new Dictionary<TProperty, int>();
            bool hasValue = false;
            TProperty currentMin = default;

            void Increment(TSource item)
            {
                var value = selector(item);
                itemValues[item] = value;
                if (valueCounts.TryGetValue(value, out var count))
                {
                    valueCounts[value] = count + 1;
                }
                else
                {
                    valueCounts[value] = 1;
                }

                if (!hasValue || value.CompareTo(currentMin) < 0)
                {
                    currentMin = value;
                    hasValue = true;
                }
            }

            void Decrement(TSource item, TProperty value)
            {
                if (!valueCounts.TryGetValue(value, out var count))
                {
                    return;
                }

                if (count == 1)
                {
                    valueCounts.Remove(value);
                    if (hasValue && value.CompareTo(currentMin) == 0)
                    {
                        RecalculateMin();
                    }
                }
                else
                {
                    valueCounts[value] = count - 1;
                }

                itemValues.Remove(item);
                if (valueCounts.Count == 0)
                {
                    hasValue = false;
                    currentMin = default;
                }
            }

            void RecalculateMin()
            {
                if (valueCounts.Count == 0)
                {
                    hasValue = false;
                    currentMin = default;
                    return;
                }

                var min = default(TProperty);
                bool first = true;
                foreach (var kvp in valueCounts.Keys)
                {
                    if (first)
                    {
                        min = kvp;
                        first = false;
                    }
                    else if (kvp.CompareTo(min) < 0)
                    {
                        min = kvp;
                    }
                }

                currentMin = min;
                hasValue = true;
            }

            void Publish()
            {
                try
                {
                    observer.OnNext(hasValue ? currentMin : default);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(
                changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            Increment(change.Item);
                            break;

                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    Increment(i);
                                }
                            }
                            else
                            {
                                Increment(change.Item);
                            }

                            break;

                        case ListChangeReason.Remove:
                            if (itemValues.TryGetValue(change.Item, out var vRemove))
                            {
                                Decrement(change.Item, vRemove);
                            }

                            break;

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    if (itemValues.TryGetValue(i, out var vR))
                                    {
                                        Decrement(i, vR);
                                    }
                                }
                            }
                            else if (itemValues.TryGetValue(change.Item, out var vR2))
                            {
                                Decrement(change.Item, vR2);
                            }

                            break;

                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null && itemValues.TryGetValue(change.PreviousItem, out var vPrev))
                            {
                                Decrement(change.PreviousItem, vPrev);
                            }

                            Increment(change.Item);

                            break;

                        case ListChangeReason.Refresh:
                            // AutoRefresh-generated refresh does not carry an item; recompute all.
                            if (itemValues.Count > 0)
                            {
                                valueCounts.Clear();
                                hasValue = false;
                                currentMin = default;
                                var keys = itemValues.Keys.ToList();
                                foreach (var it in keys)
                                {
                                    var newVal2 = selector(it);
                                    itemValues[it] = newVal2;
                                    if (valueCounts.TryGetValue(newVal2, out var c))
                                    {
                                        valueCounts[newVal2] = c + 1;
                                    }
                                    else
                                    {
                                        valueCounts[newVal2] = 1;
                                    }

                                    if (!hasValue || newVal2.CompareTo(currentMin) < 0)
                                    {
                                        currentMin = newVal2;
                                        hasValue = true;
                                    }
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            itemValues.Clear();
                            valueCounts.Clear();
                            hasValue = false;
                            currentMin = default;

                            break;
                    }
                }

                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    /// <summary>
    /// Calculates the average (mean) of a numeric property across all items in the list, reactively updating as items change.
    /// </summary>
    /// <typeparam name="TSource">The type of items in the list.</typeparam>
    /// <typeparam name="TProperty">The numeric type of the property.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="selector">Function to select the numeric property value from each item.</param>
    /// <returns>An observable that emits the current average, or 0 if empty.</returns>
    public static Observable<double> Avg<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<double>(observer =>
        {
            var itemValues = new Dictionary<TSource, double>();
            double sum = 0.0;
            int count = 0;

            void AddValue(TSource item)
            {
                var v = Convert.ToDouble(selector(item));
                itemValues[item] = v;
                sum += v;
                count += 1;
            }

            void RemoveValue(TSource item)
            {
                if (itemValues.TryGetValue(item, out var v))
                {
                    sum -= v;
                    count -= 1;
                    itemValues.Remove(item);
                }
            }

            void Publish()
            {
                try
                {
                    observer.OnNext(count == 0 ? 0.0 : sum / count);
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(
                changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            AddValue(change.Item);
                            break;

                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    AddValue(i);
                                }
                            }
                            else
                            {
                                AddValue(change.Item);
                            }

                            break;

                        case ListChangeReason.Remove:
                            RemoveValue(change.Item);
                            break;

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    RemoveValue(i);
                                }
                            }
                            else
                            {
                                RemoveValue(change.Item);
                            }

                            break;

                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                RemoveValue(change.PreviousItem);
                            }

                            AddValue(change.Item);

                            break;

                        case ListChangeReason.Refresh:
                            if (itemValues.TryGetValue(change.Item, out var oldVal))
                            {
                                var newVal = Convert.ToDouble(selector(change.Item));
                                if (Math.Abs(newVal - oldVal) > double.Epsilon)
                                {
                                    sum += newVal - oldVal;
                                    itemValues[change.Item] = newVal;
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            itemValues.Clear();
                            sum = 0.0;
                            count = 0;

                            break;
                    }
                }

                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }

    /// <summary>
    /// Calculates the population standard deviation of a numeric property across all items in the list, reactively updating as items change.
    /// </summary>
    /// <typeparam name="TSource">The type of items in the list.</typeparam>
    /// <typeparam name="TProperty">The numeric type of the property.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="selector">Function to select the numeric property value from each item.</param>
    /// <returns>An observable that emits the current standard deviation, or 0 if empty.</returns>
    public static Observable<double> StdDev<TSource, TProperty>(this Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<double>(observer =>
        {
            var itemValues = new Dictionary<TSource, double>();
            double sum = 0.0;
            double sumSquares = 0.0;
            int count = 0;

            void AddValue(TSource item)
            {
                var v = Convert.ToDouble(selector(item));
                itemValues[item] = v;
                sum += v;
                sumSquares += v * v;
                count += 1;
            }

            void RemoveValue(TSource item)
            {
                if (itemValues.TryGetValue(item, out var v))
                {
                    sum -= v;
                    sumSquares -= v * v;
                    count -= 1;
                    itemValues.Remove(item);
                }
            }

            void Publish()
            {
                try
                {
                    if (count == 0)
                    {
                        observer.OnNext(0.0);
                        return;
                    }

                    double mean = sum / count;
                    double variance = (sumSquares / count) - (mean * mean); // population variance
                    if (variance < 0)
                    {
                        variance = 0; // guard against negative due to precision
                    }

                    observer.OnNext(Math.Sqrt(variance));
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }

            return source.Subscribe(
                changes =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            AddValue(change.Item);
                            break;

                        case ListChangeReason.AddRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    AddValue(i);
                                }
                            }
                            else
                            {
                                AddValue(change.Item);
                            }

                            break;

                        case ListChangeReason.Remove:
                            RemoveValue(change.Item);
                            break;

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Count > 0)
                            {
                                foreach (var i in change.Range)
                                {
                                    RemoveValue(i);
                                }
                            }
                            else
                            {
                                RemoveValue(change.Item);
                            }

                            break;

                        case ListChangeReason.Replace:
                            if (change.PreviousItem != null)
                            {
                                RemoveValue(change.PreviousItem);
                            }

                            AddValue(change.Item);

                            break;

                        case ListChangeReason.Refresh:
                            if (itemValues.TryGetValue(change.Item, out var oldVal))
                            {
                                var newVal = Convert.ToDouble(selector(change.Item));
                                if (Math.Abs(newVal - oldVal) > double.Epsilon)
                                {
                                    sum += newVal - oldVal;
                                    sumSquares += (newVal * newVal) - (oldVal * oldVal);
                                    itemValues[change.Item] = newVal;
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            itemValues.Clear();
                            sum = 0.0;
                            sumSquares = 0.0;
                            count = 0;

                            break;
                    }
                }

                Publish();
            }, observer.OnErrorResume, observer.OnCompleted);
        });
    }
}
