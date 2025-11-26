// Port of DynamicData to R3.

namespace R3.DynamicData.Cache;

/// <summary>
/// An observable cache that tracks changes to items with unique keys.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public sealed class SourceCache<TObject, TKey> : ISourceCache<TObject, TKey>
    where TKey : notnull
{
    private readonly Func<TObject, TKey> _keySelector;
    private readonly Dictionary<TKey, TObject> _data;
    private readonly Subject<IChangeSet<TObject, TKey>> _changes;
    private readonly Subject<int> _countChanged;
    private readonly object _locker = new();
    private bool _isDisposed;

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_locker)
            {
                return _data.Count;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TKey> Keys
    {
        get
        {
            lock (_locker)
            {
                return _data.Keys.ToList();
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TObject> Items
    {
        get
        {
            lock (_locker)
            {
                return _data.Values.ToList();
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<TObject, TKey>> KeyValues
    {
        get
        {
            lock (_locker)
            {
                return _data.Select(kvp => new KeyValuePair<TObject, TKey>(kvp.Value, kvp.Key)).ToList();
            }
        }
    }

    /// <inheritdoc/>
    public Observable<int> CountChanged => _countChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="keySelector">The function to extract the key from an object.</param>
    public SourceCache(Func<TObject, TKey> keySelector)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _data = new Dictionary<TKey, TObject>();
        _changes = new Subject<IChangeSet<TObject, TKey>>();
        _countChanged = new Subject<int>();
    }

    /// <inheritdoc/>
    public Kernel.Optional<TObject> Lookup(TKey key)
    {
        lock (_locker)
        {
            return _data.TryGetValue(key, out var value)
                ? Kernel.Optional<TObject>.Some(value)
                : Kernel.Optional<TObject>.None;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TObject> Preview()
    {
        lock (_locker)
        {
            return _data.Values.ToList();
        }
    }

    /// <inheritdoc/>
    public Observable<IChangeSet<TObject, TKey>> Connect()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(ConnectImplementation);
    }

    /// <inheritdoc/>
    public Observable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate)
    {
        if (predicate == null)
        {
            return Connect();
        }

        return Observable.Create<IChangeSet<TObject, TKey>>(observer => ConnectWithPredicateImplementation(observer, predicate));
    }

    /// <inheritdoc/>
    public Observable<Change<TObject, TKey>> Watch(TKey key)
    {
        var state = new WatchState(_changes, key);
        return Observable.Create<Change<TObject, TKey>, WatchState>(
            state,
            static (observer, state) =>
        {
            return state.Changes.Subscribe(
                changes =>
                {
                    foreach (var change in changes.Where(c => EqualityComparer<TKey>.Default.Equals(c.Key, state.Key)))
                    {
                        observer.OnNext(change);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    private readonly struct WatchState
    {
        public readonly Subject<IChangeSet<TObject, TKey>> Changes;
        public readonly TKey Key;

        public WatchState(Subject<IChangeSet<TObject, TKey>> changes, TKey key)
        {
            Changes = changes;
            Key = key;
        }
    }

    /// <inheritdoc/>
    public void Edit(Action<ICacheUpdater<TObject, TKey>> updateAction)
    {
        if (updateAction == null)
        {
            throw new ArgumentNullException(nameof(updateAction));
        }

        lock (_locker)
        {
            var updater = new CacheUpdater(this);
            updateAction(updater);
            PublishChanges(updater.GetChanges());
        }
    }

    /// <inheritdoc/>
    public void AddOrUpdate(TObject item)
    {
        Edit(updater => updater.AddOrUpdate(item));
    }

    /// <inheritdoc/>
    public void AddOrUpdate(IEnumerable<TObject> items)
    {
        Edit(updater => updater.AddOrUpdate(items));
    }

    /// <inheritdoc/>
    public void Remove(TKey key)
    {
        Edit(updater => updater.Remove(key));
    }

    /// <inheritdoc/>
    public void Remove(IEnumerable<TKey> keys)
    {
        Edit(updater => updater.Remove(keys));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        Edit(updater => updater.Clear());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _changes.Dispose();
        _countChanged.Dispose();
    }

    private void PublishChanges(ChangeSet<TObject, TKey> changes)
    {
        if (changes.Count > 0)
        {
            _changes.OnNext(changes);
            _countChanged.OnNext(_data.Count);
        }
    }

    private IDisposable ConnectImplementation(Observer<IChangeSet<TObject, TKey>> observer)
    {
        // Emit initial snapshot.
        lock (_locker)
        {
            if (_data.Count > 0)
            {
                var initial = new ChangeSet<TObject, TKey>(_data.Count);
                foreach (var kvp in _data)
                {
                    initial.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, kvp.Key, kvp.Value));
                }

                observer.OnNext(initial);
            }
        }

        return _changes.Subscribe(observer.OnNext, observer.OnErrorResume, observer.OnCompleted);
    }

    private IDisposable ConnectWithPredicateImplementation(Observer<IChangeSet<TObject, TKey>> observer, Func<TObject, bool> predicate)
    {
        // Emit initial filtered snapshot.
        lock (_locker)
        {
            if (_data.Count > 0)
            {
                var initialFiltered = new ChangeSet<TObject, TKey>();
                foreach (var kvp in _data)
                {
                    if (predicate(kvp.Value))
                    {
                        initialFiltered.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Add, kvp.Key, kvp.Value));
                    }
                }

                if (initialFiltered.Count > 0)
                {
                    observer.OnNext(initialFiltered);
                }
            }
        }

        // Subscribe to future changes and filter them.
        return _changes.Subscribe(
            changes =>
            {
                var filtered = new ChangeSet<TObject, TKey>();
                foreach (var change in changes)
                {
                    if (predicate(change.Current))
                    {
                        filtered.Add(change);
                    }
                    else if (change.Reason == Kernel.ChangeReason.Update || change.Reason == Kernel.ChangeReason.Remove)
                    {
                        // If an item was previously included and now no longer matches, emit a Remove.
                        // We infer previous inclusion if the change is Remove (original cache removal) or Update with Previous value matching predicate.
                        if (change.Reason == Kernel.ChangeReason.Remove)
                        {
                            if (change.Previous.HasValue && predicate(change.Previous.Value))
                            {
                                filtered.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, change.Key, change.Current, change.Current));
                            }
                        }
                        else if (change.Reason == Kernel.ChangeReason.Update && change.Previous.HasValue && predicate(change.Previous.Value))
                        {
                            filtered.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, change.Key, change.Previous.Value, change.Previous.Value));
                        }
                    }
                }

                if (filtered.Count > 0)
                {
                    observer.OnNext(filtered);
                }
            },
            observer.OnErrorResume,
            observer.OnCompleted);
    }

    private sealed class CacheUpdater : ICacheUpdater<TObject, TKey>
    {
        private readonly SourceCache<TObject, TKey> _cache;
        private readonly ChangeSet<TObject, TKey> _changes;

        public CacheUpdater(SourceCache<TObject, TKey> cache)
        {
            _cache = cache;
            _changes = new ChangeSet<TObject, TKey>();
        }

        public int Count => _cache._data.Count;

        public IEnumerable<TKey> Keys => _cache._data.Keys;

        public IEnumerable<TObject> Items => _cache._data.Values;

        public Kernel.Optional<TObject> Lookup(TKey key)
        {
            return _cache._data.TryGetValue(key, out var value)
                ? Kernel.Optional<TObject>.Some(value)
                : Kernel.Optional<TObject>.None;
        }

        public void AddOrUpdate(TObject item)
        {
            var key = _cache._keySelector(item);
            var isUpdate = _cache._data.TryGetValue(key, out var previous);

            _cache._data[key] = item;

            var change = isUpdate
                ? new Change<TObject, TKey>(Kernel.ChangeReason.Update, key, item, previous!)
                : new Change<TObject, TKey>(Kernel.ChangeReason.Add, key, item);

            _changes.Add(change);
        }

        public void AddOrUpdate(IEnumerable<TObject> items)
        {
            foreach (var item in items)
            {
                AddOrUpdate(item);
            }
        }

        public void Remove(TKey key)
        {
            if (_cache._data.TryGetValue(key, out var value))
            {
                _cache._data.Remove(key);
                _changes.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, value, value));
            }
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            foreach (var key in keys)
            {
                Remove(key);
            }
        }

        public void RemoveKeys(Func<TKey, bool> predicate)
        {
            var keysToRemove = _cache._data.Keys.Where(predicate).ToList();
            Remove(keysToRemove);
        }

        public void RemoveItems(Func<TObject, bool> predicate)
        {
            var keysToRemove = _cache._data
                .Where(kvp => predicate(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();
            Remove(keysToRemove);
        }

        public void Clear()
        {
            var keys = _cache._data.Keys.ToList();
            foreach (var key in keys)
            {
                var value = _cache._data[key];
                _changes.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Remove, key, value, value));
            }

            _cache._data.Clear();
        }

        public void Refresh(TKey key)
        {
            if (_cache._data.TryGetValue(key, out var value))
            {
                _changes.Add(new Change<TObject, TKey>(Kernel.ChangeReason.Refresh, key, value));
            }
        }

        public void Refresh(IEnumerable<TKey> keys)
        {
            foreach (var key in keys)
            {
                Refresh(key);
            }
        }

        public ChangeSet<TObject, TKey> GetChanges() => _changes;
    }
}
