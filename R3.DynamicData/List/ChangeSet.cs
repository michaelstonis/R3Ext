// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

using System.Collections;

namespace R3.DynamicData.List;

public class ChangeSet<T> : IChangeSet<T>
{
    private readonly List<Change<T>> _changes;

    public static readonly ChangeSet<T> Empty = new ChangeSet<T>();

    public ChangeSet()
    {
        _changes = new List<Change<T>>();
    }

    public ChangeSet(int capacity)
    {
        _changes = new List<Change<T>>(capacity);
    }

    public ChangeSet(IEnumerable<Change<T>> changes)
    {
        _changes = new List<Change<T>>(changes);
    }

    public int Count => _changes.Count;

    public int TotalChanges => _changes.Count;

    public int Adds => _changes.Count(c => c.Reason == ListChangeReason.Add || c.Reason == ListChangeReason.AddRange);

    public int Removes => _changes.Count(c => c.Reason == ListChangeReason.Remove || c.Reason == ListChangeReason.RemoveRange || c.Reason == ListChangeReason.Clear);

    public int Moves => _changes.Count(c => c.Reason == ListChangeReason.Moved);

    public int Refreshes => _changes.Count(c => c.Reason == ListChangeReason.Refresh);

    public void Add(Change<T> change)
    {
        _changes.Add(change);
    }

    public void AddRange(IEnumerable<Change<T>> changes)
    {
        _changes.AddRange(changes);
    }

    public IEnumerator<Change<T>> GetEnumerator() => _changes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
