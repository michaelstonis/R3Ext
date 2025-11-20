// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

public interface ISourceList<T> : IDisposable
{
    Observable<IChangeSet<T>> Connect();

    Observable<int> CountChanged { get; }

    int Count { get; }

    IReadOnlyList<T> Items { get; }

    void Add(T item);

    void AddRange(IEnumerable<T> items);

    void Insert(int index, T item);

    void InsertRange(int index, IEnumerable<T> items);

    void Remove(T item);

    void RemoveAt(int index);

    void RemoveRange(int index, int count);

    void RemoveMany(IEnumerable<T> items);

    void Replace(T original, T replacement);

    void ReplaceAt(int index, T item);

    void Move(int originalIndex, int destinationIndex);

    void Clear();

    void Edit(Action<IListUpdater<T>> updateAction);
}

public interface IListUpdater<T>
{
    void Add(T item);

    void AddRange(IEnumerable<T> items);

    void Insert(int index, T item);

    void InsertRange(int index, IEnumerable<T> items);

    void Remove(T item);

    void RemoveAt(int index);

    void RemoveRange(int index, int count);

    void RemoveMany(IEnumerable<T> items);

    void Replace(T original, T replacement);

    void ReplaceAt(int index, T item);

    void Move(int originalIndex, int destinationIndex);

    void Clear();
}
