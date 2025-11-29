// Port of DynamicData to R3.

using R3.DynamicData.Cache;

namespace R3.DynamicData.Operators;

/// <summary>
/// Extension methods for filtering observable change sets.
/// </summary>
public static class FilterOperator
{
    /// <summary>
    /// Filters the observable change set using the specified predicate.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="predicate">The predicate to filter items.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Func<TObject, bool> predicate)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<IChangeSet<TObject, TKey>, StaticFilterState<TObject, TKey>>(
            new StaticFilterState<TObject, TKey>(source, predicate),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class StaticFilterState<TObject, TKey>
        where TKey : notnull
    {
        private readonly Observable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, bool> _predicate;
        private readonly Dictionary<TKey, TObject> _filteredData = new();

        public StaticFilterState(Observable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> predicate)
        {
            _source = source;
            _predicate = predicate;
        }

        public IDisposable Subscribe(Observer<IChangeSet<TObject, TKey>> observer)
        {
            return _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnNext(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));
        }

        private void OnNext(IChangeSet<TObject, TKey> changes, Observer<IChangeSet<TObject, TKey>> observer)
        {
            var filteredChanges = new ChangeSet<TObject, TKey>();

            foreach (var change in changes)
            {
                var key = change.Key;
                var current = change.Current;
                var matchesFilter = _predicate(current);
                var wasInFilter = _filteredData.TryGetValue(key, out var previousValue);

                switch (change.Reason)
                {
                    case Kernel.ChangeReason.Add:
                        if (matchesFilter)
                        {
                            _filteredData[key] = current;
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Add,
                                key,
                                current));
                        }

                        break;

                    case Kernel.ChangeReason.Update:
                        if (matchesFilter && wasInFilter)
                        {
                            _filteredData[key] = current;
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Update,
                                key,
                                current,
                                previousValue));
                        }
                        else if (matchesFilter && !wasInFilter)
                        {
                            _filteredData[key] = current;
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Add,
                                key,
                                current));
                        }
                        else if (!matchesFilter && wasInFilter)
                        {
                            _filteredData.Remove(key);
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Remove,
                                key,
                                previousValue,
                                previousValue));
                        }

                        break;

                    case Kernel.ChangeReason.Remove:
                        if (wasInFilter)
                        {
                            _filteredData.Remove(key);
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Remove,
                                key,
                                previousValue,
                                previousValue));
                        }

                        break;

                    case Kernel.ChangeReason.Refresh:
                        if (wasInFilter)
                        {
                            filteredChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Refresh,
                                key,
                                current));
                        }

                        break;
                }
            }

            if (filteredChanges.Count > 0)
            {
                observer.OnNext(filteredChanges);
            }
        }
    }

    /// <summary>
    /// Filters the observable change set using a dynamic predicate observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="predicateChanged">An observable that emits new predicates.</param>
    /// <returns>An observable that emits filtered change sets.</returns>
    public static Observable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        Observable<Func<TObject, bool>> predicateChanged)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicateChanged == null)
        {
            throw new ArgumentNullException(nameof(predicateChanged));
        }

        return Observable.Create<IChangeSet<TObject, TKey>, DynamicFilterState<TObject, TKey>>(
            new DynamicFilterState<TObject, TKey>(source, predicateChanged),
            static (observer, state) => state.Subscribe(observer));
    }

    private sealed class DynamicFilterState<TObject, TKey>
        where TKey : notnull
    {
        private readonly Observable<IChangeSet<TObject, TKey>> _source;
        private readonly Observable<Func<TObject, bool>> _predicateChanged;
        private readonly Dictionary<TKey, TObject> _allData = new();
        private readonly Dictionary<TKey, TObject> _filteredData = new();
        private Func<TObject, bool>? _currentPredicate;

        public DynamicFilterState(
            Observable<IChangeSet<TObject, TKey>> source,
            Observable<Func<TObject, bool>> predicateChanged)
        {
            _source = source;
            _predicateChanged = predicateChanged;
        }

        public IDisposable Subscribe(Observer<IChangeSet<TObject, TKey>> observer)
        {
            var predicateSubscription = _predicateChanged.Subscribe(
                (this, observer),
                static (predicate, tuple) => tuple.Item1.OnPredicateChanged(predicate, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));

            var sourceSubscription = _source.Subscribe(
                (this, observer),
                static (changes, tuple) => tuple.Item1.OnSourceChanged(changes, tuple.observer),
                static (ex, tuple) => tuple.observer.OnErrorResume(ex),
                static (result, tuple) => tuple.observer.OnCompleted(result));

            return Disposable.Combine(predicateSubscription, sourceSubscription);
        }

        private void OnPredicateChanged(Func<TObject, bool> predicate, Observer<IChangeSet<TObject, TKey>> observer)
        {
            _currentPredicate = predicate;

            // Re-evaluate all items with the new predicate
            var changes = new ChangeSet<TObject, TKey>();
            var newFilteredKeys = new HashSet<TKey>();

            foreach (var kvp in _allData)
            {
                var key = kvp.Key;
                var item = kvp.Value;
                var matchesFilter = _currentPredicate(item);
                var wasInFilter = _filteredData.TryGetValue(key, out _);

                if (matchesFilter)
                {
                    newFilteredKeys.Add(key);
                }

                if (matchesFilter && !wasInFilter)
                {
                    changes.Add(new Change<TObject, TKey>(
                        Kernel.ChangeReason.Add,
                        key,
                        item));
                }
                else if (!matchesFilter && wasInFilter)
                {
                    changes.Add(new Change<TObject, TKey>(
                        Kernel.ChangeReason.Remove,
                        key,
                        item,
                        item));
                }
            }

            _filteredData.Clear();
            foreach (var key in newFilteredKeys)
            {
                _filteredData[key] = _allData[key];
            }

            if (changes.Count > 0)
            {
                observer.OnNext(changes);
            }
        }

        private void OnSourceChanged(IChangeSet<TObject, TKey> changes, Observer<IChangeSet<TObject, TKey>> observer)
        {
            var outputChanges = new ChangeSet<TObject, TKey>();

            foreach (var change in changes)
            {
                var key = change.Key;
                var current = change.Current;

                switch (change.Reason)
                {
                    case Kernel.ChangeReason.Add:
                    case Kernel.ChangeReason.Update:
                        _allData[key] = current;

                        if (_currentPredicate != null)
                        {
                            var matchesFilter = _currentPredicate(current);
                            var wasInFilter = _filteredData.TryGetValue(key, out var previousValue);

                            if (matchesFilter && !wasInFilter)
                            {
                                _filteredData[key] = current;
                                outputChanges.Add(new Change<TObject, TKey>(
                                    Kernel.ChangeReason.Add,
                                    key,
                                    current));
                            }
                            else if (matchesFilter && wasInFilter)
                            {
                                _filteredData[key] = current;
                                outputChanges.Add(new Change<TObject, TKey>(
                                    Kernel.ChangeReason.Update,
                                    key,
                                    current,
                                    previousValue));
                            }
                            else if (!matchesFilter && wasInFilter)
                            {
                                _filteredData.Remove(key);
                                outputChanges.Add(new Change<TObject, TKey>(
                                    Kernel.ChangeReason.Remove,
                                    key,
                                    previousValue,
                                    previousValue));
                            }
                        }

                        break;

                    case Kernel.ChangeReason.Remove:
                        _allData.Remove(key);

                        if (_filteredData.TryGetValue(key, out var removedValue))
                        {
                            _filteredData.Remove(key);
                            outputChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Remove,
                                key,
                                removedValue,
                                removedValue));
                        }

                        break;

                    case Kernel.ChangeReason.Refresh:
                        if (_filteredData.ContainsKey(key))
                        {
                            outputChanges.Add(new Change<TObject, TKey>(
                                Kernel.ChangeReason.Refresh,
                                key,
                                current));
                        }

                        break;
                }
            }

            if (outputChanges.Count > 0)
            {
                observer.OnNext(outputChanges);
            }
        }
    }
}
