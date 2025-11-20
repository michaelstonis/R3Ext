
// Port of DynamicData to R3.

using System.Collections;

namespace R3.DynamicData.Cache;

/// <summary>
/// A collection of changes for a cache.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public sealed class ChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    where TKey : notnull
{
    private readonly List<Change<TObject, TKey>> _changes;

    /// <inheritdoc/>
    public int Capacity
    {
        get => _changes.Capacity;
        set => _changes.Capacity = value;
    }

    /// <inheritdoc/>
    public int Count => _changes.Count;

    /// <inheritdoc/>
    public int Adds { get; private set; }

    /// <inheritdoc/>
    public int Updates { get; private set; }

    /// <inheritdoc/>
    public int Removes { get; private set; }

    /// <inheritdoc/>
    public int Refreshes { get; private set; }

    /// <inheritdoc/>
    public int Moves { get; private set; }

    /// <inheritdoc/>
    public Change<TObject, TKey> this[int index] => _changes[index];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
    /// </summary>
    public ChangeSet()
    {
        _changes = new List<Change<TObject, TKey>>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    public ChangeSet(int capacity)
    {
        _changes = new List<Change<TObject, TKey>>(capacity);
    }

    /// <summary>
    /// Adds a change to the collection.
    /// </summary>
    /// <param name="change">The change to add.</param>
    public void Add(Change<TObject, TKey> change)
    {
        _changes.Add(change);

        switch (change.Reason)
        {
            case Kernel.ChangeReason.Add:
                Adds++;
                break;
            case Kernel.ChangeReason.Update:
                Updates++;
                break;
            case Kernel.ChangeReason.Remove:
                Removes++;
                break;
            case Kernel.ChangeReason.Refresh:
                Refreshes++;
                break;
            case Kernel.ChangeReason.Moved:
                Moves++;
                break;
        }
    }

    /// <summary>
    /// Adds a range of changes to the collection.
    /// </summary>
    /// <param name="changes">The changes to add.</param>
    public void AddRange(IEnumerable<Change<TObject, TKey>> changes)
    {
        foreach (var change in changes)
        {
            Add(change);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<Change<TObject, TKey>> GetEnumerator() => _changes.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
