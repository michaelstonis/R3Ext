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

        return Observable.Create<TProperty, MaxState<TSource, TProperty>>(
            new MaxState<TSource, TProperty>(source, selector),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class MaxState<TSource, TProperty>
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        private readonly Observable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, TProperty> _selector;
        private readonly Dictionary<TSource, TProperty> _itemValues = new();
        private readonly Dictionary<TProperty, int> _valueCounts = new();
        private bool _hasValue;
        private TProperty _currentMax;

        public MaxState(Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        {
            _source = source;
            _selector = selector;
        }

        public IDisposable Subscribe(Observer<TProperty> observer)
        {
            return _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnNext(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));
        }

        private void OnNext(IChangeSet<TSource> changes, Observer<TProperty> observer)
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
                        if (_itemValues.TryGetValue(change.Item, out var vRemove))
                        {
                            Decrement(change.Item, vRemove);
                        }

                        break;

                    case ListChangeReason.RemoveRange:
                        if (change.Range.Count > 0)
                        {
                            foreach (var i in change.Range)
                            {
                                if (_itemValues.TryGetValue(i, out var vR))
                                {
                                    Decrement(i, vR);
                                }
                            }
                        }
                        else if (_itemValues.TryGetValue(change.Item, out var vR2))
                        {
                            Decrement(change.Item, vR2);
                        }

                        break;

                    case ListChangeReason.Replace:
                        if (change.PreviousItem != null && _itemValues.TryGetValue(change.PreviousItem, out var vPrev))
                        {
                            Decrement(change.PreviousItem, vPrev);
                        }

                        Increment(change.Item);

                        break;

                    case ListChangeReason.Refresh:
                        // Refresh generated by AutoRefresh does not carry an item.
                        // Re-evaluate all items to reflect potential value changes.
                        if (_itemValues.Count > 0)
                        {
                            _valueCounts.Clear();
                            _hasValue = false;
                            _currentMax = default;
                            var keys = _itemValues.Keys.ToList();
                            foreach (var it in keys)
                            {
                                var newVal2 = _selector(it);
                                _itemValues[it] = newVal2;
                                _valueCounts[newVal2] = _valueCounts.TryGetValue(newVal2, out var c) ? c + 1 : 1;

                                if (!_hasValue || newVal2.CompareTo(_currentMax) > 0)
                                {
                                    _currentMax = newVal2;
                                    _hasValue = true;
                                }
                            }
                        }

                        break;

                    case ListChangeReason.Clear:
                        _itemValues.Clear();
                        _valueCounts.Clear();
                        _hasValue = false;
                        _currentMax = default;

                        break;
                }
            }

            Publish(observer);
        }

        private void Increment(TSource item)
        {
            var value = _selector(item);
            _itemValues[item] = value;
            _valueCounts[value] = _valueCounts.TryGetValue(value, out var count) ? count + 1 : 1;

            if (!_hasValue || value.CompareTo(_currentMax) > 0)
            {
                _currentMax = value;
                _hasValue = true;
            }
        }

        private void Decrement(TSource item, TProperty value)
        {
            if (!_valueCounts.TryGetValue(value, out var count))
            {
                return;
            }

            if (count == 1)
            {
                _valueCounts.Remove(value);

                // If we removed the current max, recalc.
                if (_hasValue && value.CompareTo(_currentMax) == 0)
                {
                    RecalculateMax();
                }
            }
            else
            {
                _valueCounts[value] = count - 1;
            }

            _itemValues.Remove(item);
            if (_valueCounts.Count == 0)
            {
                _hasValue = false;
                _currentMax = default;
            }
        }

        private void RecalculateMax()
        {
            if (_valueCounts.Count == 0)
            {
                _hasValue = false;
                _currentMax = default;
                return;
            }

            // Linear scan of distinct values.
            var max = default(TProperty);
            bool first = true;
            foreach (var kvp in _valueCounts.Keys)
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

            _currentMax = max;
            _hasValue = true;
        }

        private void Publish(Observer<TProperty> observer)
        {
            try
            {
                observer.OnNext(_hasValue ? _currentMax : default);
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        }
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

        return Observable.Create<TProperty, MinState<TSource, TProperty>>(
            new MinState<TSource, TProperty>(source, selector),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class MinState<TSource, TProperty>
        where TSource : notnull
        where TProperty : struct, IComparable<TProperty>
    {
        private readonly Observable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, TProperty> _selector;
        private readonly Dictionary<TSource, TProperty> _itemValues = new();
        private readonly Dictionary<TProperty, int> _valueCounts = new();
        private bool _hasValue;
        private TProperty _currentMin;

        public MinState(Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        {
            _source = source;
            _selector = selector;
        }

        public IDisposable Subscribe(Observer<TProperty> observer)
        {
            return _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnNext(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));
        }

        private void OnNext(IChangeSet<TSource> changes, Observer<TProperty> observer)
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
                        if (_itemValues.TryGetValue(change.Item, out var vRemove))
                        {
                            Decrement(change.Item, vRemove);
                        }

                        break;

                    case ListChangeReason.RemoveRange:
                        if (change.Range.Count > 0)
                        {
                            foreach (var i in change.Range)
                            {
                                if (_itemValues.TryGetValue(i, out var vR))
                                {
                                    Decrement(i, vR);
                                }
                            }
                        }
                        else if (_itemValues.TryGetValue(change.Item, out var vR2))
                        {
                            Decrement(change.Item, vR2);
                        }

                        break;

                    case ListChangeReason.Replace:
                        if (change.PreviousItem != null && _itemValues.TryGetValue(change.PreviousItem, out var vPrev))
                        {
                            Decrement(change.PreviousItem, vPrev);
                        }

                        Increment(change.Item);

                        break;

                    case ListChangeReason.Refresh:
                        // AutoRefresh-generated refresh does not carry an item; recompute all.
                        if (_itemValues.Count > 0)
                        {
                            _valueCounts.Clear();
                            _hasValue = false;
                            _currentMin = default;
                            var keys = _itemValues.Keys.ToList();
                            foreach (var it in keys)
                            {
                                var newVal2 = _selector(it);
                                _itemValues[it] = newVal2;
                                _valueCounts[newVal2] = _valueCounts.TryGetValue(newVal2, out var c) ? c + 1 : 1;

                                if (!_hasValue || newVal2.CompareTo(_currentMin) < 0)
                                {
                                    _currentMin = newVal2;
                                    _hasValue = true;
                                }
                            }
                        }

                        break;

                    case ListChangeReason.Clear:
                        _itemValues.Clear();
                        _valueCounts.Clear();
                        _hasValue = false;
                        _currentMin = default;

                        break;
                }
            }

            Publish(observer);
        }

        private void Increment(TSource item)
        {
            var value = _selector(item);
            _itemValues[item] = value;
            _valueCounts[value] = _valueCounts.TryGetValue(value, out var count) ? count + 1 : 1;

            if (!_hasValue || value.CompareTo(_currentMin) < 0)
            {
                _currentMin = value;
                _hasValue = true;
            }
        }

        private void Decrement(TSource item, TProperty value)
        {
            if (!_valueCounts.TryGetValue(value, out var count))
            {
                return;
            }

            if (count == 1)
            {
                _valueCounts.Remove(value);
                if (_hasValue && value.CompareTo(_currentMin) == 0)
                {
                    RecalculateMin();
                }
            }
            else
            {
                _valueCounts[value] = count - 1;
            }

            _itemValues.Remove(item);
            if (_valueCounts.Count == 0)
            {
                _hasValue = false;
                _currentMin = default;
            }
        }

        private void RecalculateMin()
        {
            if (_valueCounts.Count == 0)
            {
                _hasValue = false;
                _currentMin = default;
                return;
            }

            var min = default(TProperty);
            bool first = true;
            foreach (var kvp in _valueCounts.Keys)
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

            _currentMin = min;
            _hasValue = true;
        }

        private void Publish(Observer<TProperty> observer)
        {
            try
            {
                observer.OnNext(_hasValue ? _currentMin : default);
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        }
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

        return Observable.Create<double, AvgState<TSource, TProperty>>(
            new AvgState<TSource, TProperty>(source, selector),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class AvgState<TSource, TProperty>
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        private readonly Observable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, TProperty> _selector;
        private readonly Dictionary<TSource, double> _itemValues = new();
        private double _sum;
        private int _count;

        public AvgState(Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        {
            _source = source;
            _selector = selector;
        }

        public IDisposable Subscribe(Observer<double> observer)
        {
            return _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnNext(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));
        }

        private void OnNext(IChangeSet<TSource> changes, Observer<double> observer)
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
                        if (_itemValues.TryGetValue(change.Item, out var oldVal))
                        {
                            var newVal = Convert.ToDouble(_selector(change.Item));
                            if (Math.Abs(newVal - oldVal) > double.Epsilon)
                            {
                                _sum += newVal - oldVal;
                                _itemValues[change.Item] = newVal;
                            }
                        }

                        break;

                    case ListChangeReason.Clear:
                        _itemValues.Clear();
                        _sum = 0.0;
                        _count = 0;

                        break;
                }
            }

            Publish(observer);
        }

        private void AddValue(TSource item)
        {
            var v = Convert.ToDouble(_selector(item));
            _itemValues[item] = v;
            _sum += v;
            _count += 1;
        }

        private void RemoveValue(TSource item)
        {
            if (_itemValues.TryGetValue(item, out var v))
            {
                _sum -= v;
                _count -= 1;
                _itemValues.Remove(item);
            }
        }

        private void Publish(Observer<double> observer)
        {
            try
            {
                observer.OnNext(_count == 0 ? 0.0 : _sum / _count);
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        }
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

        return Observable.Create<double, StdDevState<TSource, TProperty>>(
            new StdDevState<TSource, TProperty>(source, selector),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class StdDevState<TSource, TProperty>
        where TSource : notnull
        where TProperty : struct, IConvertible
    {
        private readonly Observable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, TProperty> _selector;
        private readonly Dictionary<TSource, double> _itemValues = new();
        private double _sum;
        private double _sumSquares;
        private int _count;

        public StdDevState(Observable<IChangeSet<TSource>> source, Func<TSource, TProperty> selector)
        {
            _source = source;
            _selector = selector;
        }

        public IDisposable Subscribe(Observer<double> observer)
        {
            return _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnNext(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));
        }

        private void OnNext(IChangeSet<TSource> changes, Observer<double> observer)
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
                        if (_itemValues.TryGetValue(change.Item, out var oldVal))
                        {
                            var newVal = Convert.ToDouble(_selector(change.Item));
                            if (Math.Abs(newVal - oldVal) > double.Epsilon)
                            {
                                _sum += newVal - oldVal;
                                _sumSquares += (newVal * newVal) - (oldVal * oldVal);
                                _itemValues[change.Item] = newVal;
                            }
                        }

                        break;

                    case ListChangeReason.Clear:
                        _itemValues.Clear();
                        _sum = 0.0;
                        _sumSquares = 0.0;
                        _count = 0;

                        break;
                }
            }

            Publish(observer);
        }

        private void AddValue(TSource item)
        {
            var v = Convert.ToDouble(_selector(item));
            _itemValues[item] = v;
            _sum += v;
            _sumSquares += v * v;
            _count += 1;
        }

        private void RemoveValue(TSource item)
        {
            if (_itemValues.TryGetValue(item, out var v))
            {
                _sum -= v;
                _sumSquares -= v * v;
                _count -= 1;
                _itemValues.Remove(item);
            }
        }

        private void Publish(Observer<double> observer)
        {
            try
            {
                if (_count == 0)
                {
                    observer.OnNext(0.0);
                    return;
                }

                double mean = _sum / _count;
                double variance = (_sumSquares / _count) - (mean * mean); // population variance
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
    }
}
