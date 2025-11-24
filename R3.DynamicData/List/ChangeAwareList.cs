// Port of DynamicData to R3.

using System.Collections;

namespace R3.DynamicData.List;

/// <summary>
/// A list which captures all changes made to it.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public class ChangeAwareList<T> : IExtendedList<T>
{
    private readonly List<T> _innerList;
    private ChangeSet<T> _changes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareList{T}"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity of the list.</param>
    public ChangeAwareList(int capacity = -1)
    {
        _innerList = capacity > 0 ? new List<T>(capacity) : new List<T>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareList{T}"/> class with the specified items.
    /// </summary>
    /// <param name="items">The items to initialize the list with.</param>
    public ChangeAwareList(IEnumerable<T> items)
    {
        _innerList = items.ToList();
        if (_innerList.Count > 0)
        {
            _changes.Add(new Change<T>(ListChangeReason.AddRange, _innerList, 0));
        }
    }

    /// <summary>
    /// Gets or sets the capacity of the internal list.
    /// </summary>
    public int Capacity
    {
        get => _innerList.Capacity;
        set => _innerList.Capacity = value;
    }

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count => _innerList.Count;

    /// <summary>
    /// Gets a value indicating whether the list is read-only. Always returns false.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get or set.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index]
    {
        get => _innerList[index];
        set
        {
            var previous = _innerList[index];
            _innerList[index] = value;
            _changes.Add(new Change<T>(ListChangeReason.Replace, value, previous, index));
        }
    }

    /// <summary>
    /// Adds an item to the list.
    /// </summary>
    public void Add(T item)
    {
        var index = _innerList.Count;
        _innerList.Add(item);
        _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
    }

    /// <summary>
    /// Adds a collection of items to the list.
    /// </summary>
    public void AddRange(IEnumerable<T> collection)
    {
        var items = collection.ToList();
        if (items.Count == 0)
        {
            return;
        }

        var index = _innerList.Count;
        _innerList.AddRange(items);
        _changes.Add(new Change<T>(ListChangeReason.AddRange, items, index));
    }

    /// <summary>
    /// Captures all changes accumulated since the last call and resets the change buffer.
    /// </summary>
    /// <returns>A change set containing all accumulated changes.</returns>
    public IChangeSet<T> CaptureChanges()
    {
        if (_changes.Count == 0)
        {
            return ChangeSet<T>.Empty;
        }

        var returnValue = _changes;
        _changes = new ChangeSet<T>();
        return returnValue;
    }

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    public void Clear()
    {
        if (_innerList.Count == 0)
        {
            return;
        }

        var toRemove = _innerList.ToList();
        _innerList.Clear();
        _changes.Add(new Change<T>(ListChangeReason.Clear, toRemove, 0));
    }

    /// <summary>
    /// Determines whether the list contains a specific item.
    /// </summary>
    public bool Contains(T item) => _innerList.Contains(item);

    /// <summary>
    /// Copies the elements of the list to an array.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex) => _innerList.CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator that iterates through the list.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => _innerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Determines the index of a specific item in the list.
    /// </summary>
    public int IndexOf(T item) => _innerList.IndexOf(item);

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    public void Insert(int index, T item)
    {
        _innerList.Insert(index, item);
        _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
    }

    /// <summary>
    /// Inserts a collection of items at the specified index.
    /// </summary>
    public void InsertRange(IEnumerable<T> collection, int index)
    {
        var items = collection.ToList();
        if (items.Count == 0)
        {
            return;
        }

        _innerList.InsertRange(index, items);
        _changes.Add(new Change<T>(ListChangeReason.AddRange, items, index));
    }

    /// <summary>
    /// Moves an item from one index to another.
    /// </summary>
    public void Move(int original, int destination)
    {
        var item = _innerList[original];
        _innerList.RemoveAt(original);
        _innerList.Insert(destination, item);
        _changes.Add(new Change<T>(ListChangeReason.Moved, item, destination, original));
    }

    /// <summary>
    /// Removes the first occurrence of a specific item from the list.
    /// </summary>
    public bool Remove(T item)
    {
        var index = _innerList.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        var item = _innerList[index];
        _innerList.RemoveAt(index);
        _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
    }

    /// <summary>
    /// Removes a range of items from the list.
    /// </summary>
    public void RemoveRange(int index, int count)
    {
        if (count == 0)
        {
            return;
        }

        var toRemove = _innerList.Skip(index).Take(count).ToList();
        _innerList.RemoveRange(index, count);
        _changes.Add(new Change<T>(ListChangeReason.RemoveRange, toRemove, index));
    }
}
