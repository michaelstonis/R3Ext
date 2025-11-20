// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

public sealed class SourceList<T> : ISourceList<T>
{
    private readonly List<T> _items = new();
    private readonly object _lock = new();
    private readonly Subject<IChangeSet<T>> _changes = new();
    private readonly Subject<int> _countChanged = new();
    private bool _disposed;

    public Observable<IChangeSet<T>> Connect()
    {
        return Observable.Create<IChangeSet<T>>(observer =>
        {
            lock (_lock)
            {
                if (_items.Count > 0)
                {
                    var initialChanges = new ChangeSet<T>(_items.Count);
                    for (int i = 0; i < _items.Count; i++)
                    {
                        initialChanges.Add(new Change<T>(ListChangeReason.Add, _items[i], i));
                    }

                    observer.OnNext(initialChanges);
                }
            }

            return _changes.Subscribe(observer);
        });
    }

    public Observable<int> CountChanged => _countChanged;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }
    }

    public IReadOnlyList<T> Items
    {
        get
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _items.Add(item);
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Add, item, _items.Count - 1));
            PublishChanges(changeSet);
        }
    }

    public void AddRange(IEnumerable<T> items)
    {
        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            var startIndex = _items.Count;
            _items.AddRange(itemsList);
            var changeSet = new ChangeSet<T>(itemsList.Count);
            for (int i = 0; i < itemsList.Count; i++)
            {
                changeSet.Add(new Change<T>(ListChangeReason.AddRange, itemsList[i], startIndex + i));
            }

            PublishChanges(changeSet);
        }
    }

    public void Insert(int index, T item)
    {
        lock (_lock)
        {
            _items.Insert(index, item);
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Add, item, index));
            PublishChanges(changeSet);
        }
    }

    public void InsertRange(int index, IEnumerable<T> items)
    {
        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            _items.InsertRange(index, itemsList);
            var changeSet = new ChangeSet<T>(itemsList.Count);
            for (int i = 0; i < itemsList.Count; i++)
            {
                changeSet.Add(new Change<T>(ListChangeReason.AddRange, itemsList[i], index + i));
            }

            PublishChanges(changeSet);
        }
    }

    public void Remove(T item)
    {
        lock (_lock)
        {
            var index = _items.IndexOf(item);
            if (index >= 0)
            {
                _items.RemoveAt(index);
                var changeSet = new ChangeSet<T>(1);
                changeSet.Add(new Change<T>(ListChangeReason.Remove, item, index));
                PublishChanges(changeSet);
            }
        }
    }

    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Remove, item, index));
            PublishChanges(changeSet);
        }
    }

    public void RemoveRange(int index, int count)
    {
        if (count == 0)
        {
            return;
        }

        lock (_lock)
        {
            var removed = _items.GetRange(index, count);
            _items.RemoveRange(index, count);
            var changeSet = new ChangeSet<T>(count);
            for (int i = 0; i < removed.Count; i++)
            {
                changeSet.Add(new Change<T>(ListChangeReason.RemoveRange, removed[i], index + i));
            }

            PublishChanges(changeSet);
        }
    }

    public void RemoveMany(IEnumerable<T> items)
    {
        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            var changeSet = new ChangeSet<T>();
            foreach (var item in itemsList)
            {
                var index = _items.IndexOf(item);
                if (index >= 0)
                {
                    _items.RemoveAt(index);
                    changeSet.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
            }

            if (changeSet.Count > 0)
            {
                PublishChanges(changeSet);
            }
        }
    }

    public void Replace(T original, T replacement)
    {
        lock (_lock)
        {
            var index = _items.IndexOf(original);
            if (index >= 0)
            {
                _items[index] = replacement;
                var changeSet = new ChangeSet<T>(1);
                changeSet.Add(new Change<T>(ListChangeReason.Replace, replacement, index));
                PublishChanges(changeSet);
            }
        }
    }

    public void ReplaceAt(int index, T item)
    {
        lock (_lock)
        {
            _items[index] = item;
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Replace, item, index));
            PublishChanges(changeSet);
        }
    }

    public void Move(int originalIndex, int destinationIndex)
    {
        lock (_lock)
        {
            var item = _items[originalIndex];
            _items.RemoveAt(originalIndex);
            _items.Insert(destinationIndex, item);
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Moved, item, destinationIndex, originalIndex));
            PublishChanges(changeSet);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_items.Count == 0)
            {
                return;
            }

            var removed = _items.ToList();
            _items.Clear();
            var changeSet = new ChangeSet<T>(1);
            changeSet.Add(new Change<T>(ListChangeReason.Clear, removed, -1));
            PublishChanges(changeSet);
        }
    }

    public void Edit(Action<IListUpdater<T>> updateAction)
    {
        lock (_lock)
        {
            var updater = new ListUpdater(this);
            updateAction(updater);
            if (updater.Changes.Count > 0)
            {
                PublishChanges(updater.Changes);
            }
        }
    }

    private void PublishChanges(ChangeSet<T> changeSet)
    {
        _changes.OnNext(changeSet);
        _countChanged.OnNext(_items.Count);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _changes.Dispose();
        _countChanged.Dispose();
    }

    private sealed class ListUpdater : IListUpdater<T>
    {
        private readonly SourceList<T> _source;

        public ChangeSet<T> Changes { get; } = new();

        public ListUpdater(SourceList<T> source)
        {
            _source = source;
        }

        public void Add(T item)
        {
            _source._items.Add(item);
            Changes.Add(new Change<T>(ListChangeReason.Add, item, _source._items.Count - 1));
        }

        public void AddRange(IEnumerable<T> items)
        {
            var itemsList = items.ToList();
            if (itemsList.Count == 0)
            {
                return;
            }

            var startIndex = _source._items.Count;
            _source._items.AddRange(itemsList);
            for (int i = 0; i < itemsList.Count; i++)
            {
                Changes.Add(new Change<T>(ListChangeReason.AddRange, itemsList[i], startIndex + i));
            }
        }

        public void Insert(int index, T item)
        {
            _source._items.Insert(index, item);
            Changes.Add(new Change<T>(ListChangeReason.Add, item, index));
        }

        public void InsertRange(int index, IEnumerable<T> items)
        {
            var itemsList = items.ToList();
            if (itemsList.Count == 0)
            {
                return;
            }

            _source._items.InsertRange(index, itemsList);
            for (int i = 0; i < itemsList.Count; i++)
            {
                Changes.Add(new Change<T>(ListChangeReason.AddRange, itemsList[i], index + i));
            }
        }

        public void Remove(T item)
        {
            var index = _source._items.IndexOf(item);
            if (index >= 0)
            {
                _source._items.RemoveAt(index);
                Changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
            }
        }

        public void RemoveAt(int index)
        {
            var item = _source._items[index];
            _source._items.RemoveAt(index);
            Changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
        }

        public void RemoveRange(int index, int count)
        {
            if (count == 0)
            {
                return;
            }

            var removed = _source._items.GetRange(index, count);
            _source._items.RemoveRange(index, count);
            for (int i = 0; i < removed.Count; i++)
            {
                Changes.Add(new Change<T>(ListChangeReason.RemoveRange, removed[i], index + i));
            }
        }

        public void RemoveMany(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                var index = _source._items.IndexOf(item);
                if (index >= 0)
                {
                    _source._items.RemoveAt(index);
                    Changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
            }
        }

        public void Replace(T original, T replacement)
        {
            var index = _source._items.IndexOf(original);
            if (index >= 0)
            {
                _source._items[index] = replacement;
                Changes.Add(new Change<T>(ListChangeReason.Replace, replacement, index));
            }
        }

        public void ReplaceAt(int index, T item)
        {
            _source._items[index] = item;
            Changes.Add(new Change<T>(ListChangeReason.Replace, item, index));
        }

        public void Move(int originalIndex, int destinationIndex)
        {
            var item = _source._items[originalIndex];
            _source._items.RemoveAt(originalIndex);
            _source._items.Insert(destinationIndex, item);
            Changes.Add(new Change<T>(ListChangeReason.Moved, item, destinationIndex, originalIndex));
        }

        public void Clear()
        {
            if (_source._items.Count == 0)
            {
                return;
            }

            var removed = _source._items.ToList();
            _source._items.Clear();
            Changes.Add(new Change<T>(ListChangeReason.Clear, removed, -1));
        }
    }
}
