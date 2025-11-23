// Port of DynamicData to R3.
// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Extensions for creating observable caches.
/// </summary>
public static class ObservableCacheExtensions
{
    /// <summary>
    /// Converts a SourceCache to a read-only IObservableCache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source cache.</param>
    /// <returns>A read-only observable cache.</returns>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new ObservableCacheAdapter<TObject, TKey>(source);
    }

    /// <summary>
    /// Converts an observable of change sets to a read-only IObservableCache.
    /// This materializes the change set stream into a queryable cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable of change sets.</param>
    /// <param name="applyLocking">Whether to apply locking (not used in this implementation).</param>
    /// <returns>A read-only observable cache.</returns>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(
        this Observable<IChangeSet<TObject, TKey>> source,
        bool applyLocking = true)
        where TKey : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new MaterializingObservableCache<TObject, TKey>(source);
    }

    private sealed class ObservableCacheAdapter<TObject, TKey> : IObservableCache<TObject, TKey>
        where TKey : notnull
    {
        private readonly ISourceCache<TObject, TKey> _source;

        public ObservableCacheAdapter(ISourceCache<TObject, TKey> source)
        {
            _source = source;
        }

        public int Count => _source.Count;

        public IReadOnlyList<TObject> Items => _source.Items.ToList();

        public IReadOnlyList<TKey> Keys => _source.Keys.ToList();

        public IReadOnlyDictionary<TKey, TObject> KeyValues
        {
            get
            {
                var dict = new Dictionary<TKey, TObject>();
                foreach (var kvp in _source.KeyValues)
                {
                    dict[kvp.Value] = kvp.Key;
                }

                return dict;
            }
        }

        public Observable<int> CountChanged => _source.CountChanged;

        public Kernel.Optional<TObject> Lookup(TKey key) => _source.Lookup(key);

        public Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        {
            if (predicate == null)
            {
                return _source.Connect();
            }

            return _source.Connect(predicate);
        }

        public Observable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        {
            // Preview doesn't include initial state, so skip the first emission if there's data
            var connected = Connect(predicate, suppressEmptyChangeSets: true);
            return connected.Skip(1);
        }

        public Observable<Change<TObject, TKey>> Watch(TKey key) => _source.Watch(key);

        public void Dispose()
        {
            // Don't dispose the source cache - we're just wrapping it
        }
    }

    /// <summary>
    /// A cache that materializes changes from an observable of change sets.
    /// This implementation subscribes to the source observable and maintains an internal dictionary
    /// that is updated as change sets are received.
    /// </summary>
    private sealed class MaterializingObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, TObject> _cache = new();
        private readonly Subject<IChangeSet<TObject, TKey>> _changesSubject = new();
        private readonly Subject<int> _countChangedSubject = new();
        private readonly IDisposable _subscription;
        private int _count;

        public MaterializingObservableCache(Observable<IChangeSet<TObject, TKey>> source)
        {
            // Subscribe to the source and materialize changes into our internal cache
            _subscription = source.Subscribe(
                changeSet =>
                {
                    // Apply changes to our internal cache
                    foreach (var change in changeSet)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                _cache[change.Key] = change.Current;
                                break;
                            case ChangeReason.Remove:
                                _cache.TryRemove(change.Key, out _);
                                break;
                            case ChangeReason.Refresh:
                                // Refresh doesn't change the cache content
                                break;
                        }
                    }

                    // Update count
                    var newCount = _cache.Count;
                    if (newCount != _count)
                    {
                        _count = newCount;
                        _countChangedSubject.OnNext(_count);
                    }

                    // Emit the change set
                    _changesSubject.OnNext(changeSet);
                });
        }

        public int Count => _cache.Count;

        public IReadOnlyList<TObject> Items => _cache.Values.ToList();

        public IReadOnlyList<TKey> Keys => _cache.Keys.ToList();

        public IReadOnlyDictionary<TKey, TObject> KeyValues => new Dictionary<TKey, TObject>(_cache);

        public Observable<int> CountChanged => _countChangedSubject.AsObservable();

        public Kernel.Optional<TObject> Lookup(TKey key)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                return Kernel.Optional<TObject>.Some(value);
            }

            return Kernel.Optional<TObject>.None;
        }

        public Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        {
            return Observable.Defer(() =>
            {
                // Get initial snapshot
                var initialData = _cache.ToList();
                var initial = new ChangeSet<TObject, TKey>(initialData.Count);

                foreach (var kvp in initialData)
                {
                    if (predicate == null || predicate(kvp.Value))
                    {
                        initial.Add(new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                }

                // Create the observable sequence
                Observable<IChangeSet<TObject, TKey>> result;

                if (initial.Count > 0)
                {
                    result = Observable.Return<IChangeSet<TObject, TKey>>(initial).Concat(_changesSubject.AsObservable());
                }
                else
                {
                    result = _changesSubject.AsObservable();
                }

                // Apply filter if needed
                if (predicate != null)
                {
                    result = result.Select(cs =>
                    {
                        var filtered = new ChangeSet<TObject, TKey>();
                        foreach (var change in cs)
                        {
                            var matches = predicate(change.Current);
                            if (matches)
                            {
                                filtered.Add(change);
                            }
                        }

                        return (IChangeSet<TObject, TKey>)filtered;
                    });
                }

                // Suppress empty change sets if requested
                if (suppressEmptyChangeSets)
                {
                    result = result.Where(cs => cs.Count > 0);
                }

                return result;
            });
        }

        public Observable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        {
            // Preview doesn't include initial state
            var result = _changesSubject.AsObservable();

            if (predicate != null)
            {
                result = result.Select(cs =>
                {
                    var filtered = new ChangeSet<TObject, TKey>();
                    foreach (var change in cs)
                    {
                        var matches = predicate(change.Current);
                        if (matches)
                        {
                            filtered.Add(change);
                        }
                    }

                    return (IChangeSet<TObject, TKey>)filtered;
                });
            }

            return result;
        }

        public Observable<Change<TObject, TKey>> Watch(TKey key)
        {
            var state = new WatchState(_cache, key, _changesSubject);
            return Observable.Create<Change<TObject, TKey>, WatchState>(state, static (observer, state) =>
            {
                // Send initial value if present
                if (state.Cache.TryGetValue(state.Key, out var value))
                {
                    observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, state.Key, value));
                }

                // Subscribe to changes for this key
                return state.ChangesSubject.Subscribe(changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        if (EqualityComparer<TKey>.Default.Equals(change.Key, state.Key))
                        {
                            observer.OnNext(change);
                        }
                    }
                });
            });
        }

        private readonly struct WatchState
        {
            public readonly ConcurrentDictionary<TKey, TObject> Cache;
            public readonly TKey Key;
            public readonly Subject<IChangeSet<TObject, TKey>> ChangesSubject;

            public WatchState(
                ConcurrentDictionary<TKey, TObject> cache,
                TKey key,
                Subject<IChangeSet<TObject, TKey>> changesSubject)
            {
                Cache = cache;
                Key = key;
                ChangesSubject = changesSubject;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _changesSubject.Dispose();
            _countChangedSubject.Dispose();
        }
    }
}
