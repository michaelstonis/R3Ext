// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// A reactive list that publishes changes as an observable change set.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public interface ISourceList<T> : IDisposable
{
    /// <summary>
    /// Connects to the list and observes all changes.
    /// </summary>
    /// <returns>An observable that emits change sets.</returns>
    Observable<IChangeSet<T>> Connect();

    /// <summary>
    /// Gets an observable that emits the current count whenever it changes.
    /// </summary>
    Observable<int> CountChanged { get; }

    /// <summary>
    /// Gets the current count of items in the list.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a read-only snapshot of the current items in the list.
    /// </summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void Add(T item);

    /// <summary>
    /// Adds a range of items to the end of the list.
    /// </summary>
    /// <param name="items">The items to add.</param>
    void AddRange(IEnumerable<T> items);

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    void Insert(int index, T item);

    /// <summary>
    /// Inserts a range of items at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the items.</param>
    /// <param name="items">The items to insert.</param>
    void InsertRange(int index, IEnumerable<T> items);

    /// <summary>
    /// Removes the first occurrence of the specified item.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    void Remove(T item);

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    void RemoveAt(int index);

    /// <summary>
    /// Removes a range of items starting at the specified index.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range to remove.</param>
    /// <param name="count">The number of items to remove.</param>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Removes multiple items from the list.
    /// </summary>
    /// <param name="items">The items to remove.</param>
    void RemoveMany(IEnumerable<T> items);

    /// <summary>
    /// Replaces the first occurrence of an item with a replacement.
    /// </summary>
    /// <param name="original">The item to replace.</param>
    /// <param name="replacement">The replacement item.</param>
    void Replace(T original, T replacement);

    /// <summary>
    /// Replaces the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to replace.</param>
    /// <param name="item">The replacement item.</param>
    void ReplaceAt(int index, T item);

    /// <summary>
    /// Moves an item from one index to another.
    /// </summary>
    /// <param name="originalIndex">The zero-based index of the item to move.</param>
    /// <param name="destinationIndex">The zero-based index to move the item to.</param>
    void Move(int originalIndex, int destinationIndex);

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    void Clear();

    /// <summary>
    /// Performs a batch edit operation on the list.
    /// </summary>
    /// <param name="updateAction">The action that performs the batch updates.</param>
    void Edit(Action<IListUpdater<T>> updateAction);
}

/// <summary>
/// Provides methods to update a source list within a batch edit operation.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public interface IListUpdater<T>
{
    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void Add(T item);

    /// <summary>
    /// Adds a range of items to the end of the list.
    /// </summary>
    /// <param name="items">The items to add.</param>
    void AddRange(IEnumerable<T> items);

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    void Insert(int index, T item);

    /// <summary>
    /// Inserts a range of items at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the items.</param>
    /// <param name="items">The items to insert.</param>
    void InsertRange(int index, IEnumerable<T> items);

    /// <summary>
    /// Removes the first occurrence of the specified item.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    void Remove(T item);

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    void RemoveAt(int index);

    /// <summary>
    /// Removes a range of items starting at the specified index.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range to remove.</param>
    /// <param name="count">The number of items to remove.</param>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Removes multiple items from the list.
    /// </summary>
    /// <param name="items">The items to remove.</param>
    void RemoveMany(IEnumerable<T> items);

    /// <summary>
    /// Replaces the first occurrence of an item with a replacement.
    /// </summary>
    /// <param name="original">The item to replace.</param>
    /// <param name="replacement">The replacement item.</param>
    void Replace(T original, T replacement);

    /// <summary>
    /// Replaces the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to replace.</param>
    /// <param name="item">The replacement item.</param>
    void ReplaceAt(int index, T item);

    /// <summary>
    /// Moves an item from one index to another.
    /// </summary>
    /// <param name="originalIndex">The zero-based index of the item to move.</param>
    /// <param name="destinationIndex">The zero-based index to move the item to.</param>
    void Move(int originalIndex, int destinationIndex);

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    void Clear();
}
