// Port of DynamicData to R3.

using System.Collections;

namespace R3.DynamicData.List;

/// <summary>
/// Represents a collection of changes to a list.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
public class ChangeSet<T> : IChangeSet<T>
{
    private readonly List<Change<T>> _changes;

    /// <summary>
    /// Gets an empty change set.
    /// </summary>
    public static readonly ChangeSet<T> Empty = new ChangeSet<T>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class.
    /// </summary>
    public ChangeSet()
    {
        _changes = new List<Change<T>>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity.</param>
    public ChangeSet(int capacity)
    {
        _changes = new List<Change<T>>(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSet{T}"/> class with the specified changes.
    /// </summary>
    /// <param name="changes">The initial changes.</param>
    public ChangeSet(IEnumerable<Change<T>> changes)
    {
        _changes = new List<Change<T>>(changes);
    }

    /// <summary>
    /// Gets the number of changes in this change set.
    /// </summary>
    public int Count => _changes.Count;

    /// <summary>
    /// Gets the total number of changes.
    /// </summary>
    public int TotalChanges => _changes.Count;

    /// <summary>
    /// Gets the total number of items added.
    /// </summary>
    public int Adds => _changes.Sum(c =>
    {
        if (c.Reason == ListChangeReason.Add)
        {
            return 1;
        }

        if (c.Reason == ListChangeReason.AddRange)
        {
            // Support both aggregated range (Range set) and per-item AddRange entries (Range null)
            return c.Range?.Count ?? 1;
        }

        return 0;
    });

    /// <summary>
    /// Gets the total number of items removed.
    /// </summary>
    public int Removes => _changes.Sum(c =>
    {
        if (c.Reason == ListChangeReason.Remove)
        {
            return 1;
        }

        if (c.Reason == ListChangeReason.RemoveRange || c.Reason == ListChangeReason.Clear)
        {
            // Support both aggregated range (Range set) and per-item RemoveRange entries (Range null)
            return c.Range?.Count ?? 1;
        }

        return 0;
    });

    /// <summary>
    /// Gets the number of move operations.
    /// </summary>
    public int Moves => _changes.Count(c => c.Reason == ListChangeReason.Moved);

    /// <summary>
    /// Gets the number of refresh operations.
    /// </summary>
    public int Refreshes => _changes.Count(c => c.Reason == ListChangeReason.Refresh);

    /// <summary>
    /// Adds a change to this change set.
    /// </summary>
    /// <param name="change">The change to add.</param>
    public void Add(Change<T> change)
    {
        _changes.Add(change);
    }

    /// <summary>
    /// Adds multiple changes to this change set.
    /// </summary>
    /// <param name="changes">The changes to add.</param>
    public void AddRange(IEnumerable<Change<T>> changes)
    {
        _changes.AddRange(changes);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the changes.
    /// </summary>
    /// <returns>An enumerator for the changes.</returns>
    public IEnumerator<Change<T>> GetEnumerator() => _changes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
