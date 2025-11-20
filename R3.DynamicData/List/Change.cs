// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.List;

/// <summary>
/// Represents a single change to a list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public readonly struct Change<T> : IEquatable<Change<T>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct.
    /// </summary>
    public Change(ListChangeReason reason, T item, int index)
        : this(reason, item, index, -1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct for a move.
    /// </summary>
    public Change(ListChangeReason reason, T item, int currentIndex, int previousIndex)
    {
        Reason = reason;
        Item = item;
        CurrentIndex = currentIndex;
        PreviousIndex = previousIndex;
    }

    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ListChangeReason Reason { get; }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public T Item { get; }

    /// <summary>
    /// Gets the current index.
    /// </summary>
    public int CurrentIndex { get; }

    /// <summary>
    /// Gets the previous index (for moves).
    /// </summary>
    public int PreviousIndex { get; }

    public static bool operator ==(Change<T> left, Change<T> right) => left.Equals(right);

    public static bool operator !=(Change<T> left, Change<T> right) => !left.Equals(right);

    public bool Equals(Change<T> other) =>
        Reason == other.Reason &&
        EqualityComparer<T>.Default.Equals(Item, other.Item) &&
        CurrentIndex == other.CurrentIndex &&
        PreviousIndex == other.PreviousIndex;

    public override bool Equals(object? obj) => obj is Change<T> change && Equals(change);

    public override int GetHashCode()
    {
#if NETSTANDARD2_0
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Reason.GetHashCode();
            hash = (hash * 31) + (Item?.GetHashCode() ?? 0);
            hash = (hash * 31) + CurrentIndex.GetHashCode();
            hash = (hash * 31) + PreviousIndex.GetHashCode();
            return hash;
        }
#else
        return HashCode.Combine(Reason, Item, CurrentIndex, PreviousIndex);
#endif
    }

    public override string ToString() =>
        Reason == ListChangeReason.Moved
            ? $"{Reason} {Item} from index {PreviousIndex} to {CurrentIndex}"
            : $"{Reason} {Item} at index {CurrentIndex}";
}
