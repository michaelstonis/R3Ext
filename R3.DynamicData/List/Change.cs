// Port of DynamicData to R3.

using System.Collections.Generic;

namespace R3.DynamicData.List;

/// <summary>
/// Represents a single change to a list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public readonly struct Change<T> : IEquatable<Change<T>>
{
    private readonly IReadOnlyList<T>? _range;

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
        PreviousItem = default;
        _range = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct for a range operation.
    /// </summary>
    public Change(ListChangeReason reason, IEnumerable<T> items, int index)
    {
        if (reason != ListChangeReason.AddRange && reason != ListChangeReason.RemoveRange && reason != ListChangeReason.Clear)
        {
            throw new ArgumentException("ListChangeReason must be a range type (AddRange, RemoveRange, or Clear)");
        }

        Reason = reason;
        Item = default!;
        CurrentIndex = index;
        PreviousIndex = -1;
        PreviousItem = default;
        _range = items is IReadOnlyList<T> list ? list : items.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct for a replace operation.
    /// </summary>
    public Change(ListChangeReason reason, T item, T? previousItem, int index)
    {
        if (reason != ListChangeReason.Replace)
        {
            throw new ArgumentException("This constructor is only for Replace operations");
        }

        Reason = reason;
        Item = item;
        CurrentIndex = index;
        PreviousIndex = -1;
        PreviousItem = previousItem;
        _range = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> struct for a refresh operation.
    /// </summary>
    public Change(ListChangeReason reason)
    {
        if (reason != ListChangeReason.Refresh)
        {
            throw new ArgumentException("This constructor is only for Refresh operations");
        }

        Reason = reason;
        Item = default!;
        CurrentIndex = -1;
        PreviousIndex = -1;
        PreviousItem = default;
        _range = null;
    }

    /// <summary>
    /// Gets a refresh change.
    /// </summary>
    public static Change<T> Refresh => new Change<T>(ListChangeReason.Refresh);

    /// <summary>
    /// Gets the current item.
    /// </summary>
    public T Current => Item;

    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ListChangeReason Reason { get; }

    /// <summary>
    /// Gets the item (for single-item changes).
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

    /// <summary>
    /// Gets the previous item (for replace operations).
    /// </summary>
    public T? PreviousItem { get; }

    /// <summary>
    /// Gets the range of items (for range operations).
    /// </summary>
    public IReadOnlyList<T> Range => _range ?? Array.Empty<T>();

    public static bool operator ==(Change<T> left, Change<T> right) => left.Equals(right);

    public static bool operator !=(Change<T> left, Change<T> right) => !left.Equals(right);

    public bool Equals(Change<T> other)
    {
        if (Reason != other.Reason || CurrentIndex != other.CurrentIndex || PreviousIndex != other.PreviousIndex)
        {
            return false;
        }

        if (!EqualityComparer<T>.Default.Equals(Item, other.Item))
        {
            return false;
        }

        if (!EqualityComparer<T?>.Default.Equals(PreviousItem, other.PreviousItem))
        {
            return false;
        }

        // For range operations, compare the range contents
        if (_range != null || other._range != null)
        {
            if (_range == null || other._range == null)
            {
                return false;
            }

            if (_range.Count != other._range.Count)
            {
                return false;
            }

            for (int i = 0; i < _range.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(_range[i], other._range[i]))
                {
                    return false;
                }
            }
        }

        return true;
    }

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
            hash = (hash * 31) + (PreviousItem?.GetHashCode() ?? 0);
            if (_range != null)
            {
                hash = (hash * 31) + _range.Count.GetHashCode();
            }

            return hash;
        }
#else
        var hash = HashCode.Combine(Reason, Item, CurrentIndex, PreviousIndex, PreviousItem);
        if (_range != null)
        {
            hash = HashCode.Combine(hash, _range.Count);
        }

        return hash;
#endif
    }

    public override string ToString() =>
        Reason == ListChangeReason.Moved
            ? $"{Reason} {Item} from index {PreviousIndex} to {CurrentIndex}"
            : Reason == ListChangeReason.Replace
                ? $"{Reason} {PreviousItem} with {Item} at index {CurrentIndex}"
                : (_range != null && _range.Count > 0)
                    ? $"{Reason} {_range.Count} items at index {CurrentIndex}"
                    : $"{Reason} {Item} at index {CurrentIndex}";
}
