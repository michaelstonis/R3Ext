// Port of DynamicData to R3.

using R3.DynamicData.Kernel;

namespace R3.DynamicData.Cache;

/// <summary>
/// Represents a single change to a cached item.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public readonly struct Change<TObject, TKey> : IEquatable<Change<TObject, TKey>>
    where TKey : notnull
{
    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ChangeReason Reason { get; }

    /// <summary>
    /// Gets the key of the changed item.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public TObject Current { get; }

    /// <summary>
    /// Gets the previous value (for updates and removes).
    /// </summary>
    public Optional<TObject> Previous { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct for an add operation.
    /// </summary>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="key">The key of the item.</param>
    /// <param name="current">The current value.</param>
    public Change(ChangeReason reason, TKey key, TObject current)
    {
        Reason = reason;
        Key = key;
        Current = current;
        Previous = Optional<TObject>.None;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct for an update/remove operation.
    /// </summary>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="key">The key of the item.</param>
    /// <param name="current">The current value.</param>
    /// <param name="previous">The previous value.</param>
    public Change(ChangeReason reason, TKey key, TObject current, TObject previous)
    {
        Reason = reason;
        Key = key;
        Current = current;
        Previous = Optional<TObject>.Some(previous);
    }

    /// <summary>
    /// Determines whether the specified Change is equal to the current Change.
    /// </summary>
    /// <param name="other">The Change to compare with the current Change.</param>
    /// <returns>true if the specified Change is equal to the current Change; otherwise, false.</returns>
    public bool Equals(Change<TObject, TKey> other) =>
        EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
        Reason == other.Reason &&
        EqualityComparer<TObject>.Default.Equals(Current, other.Current) &&
        Previous.Equals(other.Previous);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Change<TObject, TKey> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (Key?.GetHashCode() ?? 0);
            hash = (hash * 31) + Reason.GetHashCode();
            hash = (hash * 31) + (Current?.GetHashCode() ?? 0);
            hash = (hash * 31) + Previous.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// Determines whether two specified instances of Change are equal.
    /// </summary>
    /// <param name="left">The first Change to compare.</param>
    /// <param name="right">The second Change to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(Change<TObject, TKey> left, Change<TObject, TKey> right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two specified instances of Change are not equal.
    /// </summary>
    /// <param name="left">The first Change to compare.</param>
    /// <param name="right">The second Change to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(Change<TObject, TKey> left, Change<TObject, TKey> right) =>
        !left.Equals(right);
}
