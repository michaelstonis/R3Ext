// Copyright (c) 2025 Michael Stonis. All rights reserved.
// Port of DynamicData to R3.

namespace R3.DynamicData.Kernel;

/// <summary>
/// Represents an optional value that may or may not be present.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct Optional<T> : IEquatable<Optional<T>>
{
    private readonly T _value;
    private readonly bool _hasValue;

    /// <summary>
    /// Gets a value indicating whether this optional has a value.
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// Gets the value if present, otherwise throws.
    /// </summary>
    public T Value => _hasValue ? _value : throw new InvalidOperationException("Optional has no value");

    private Optional(T value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    /// <summary>
    /// Gets an Optional with no value.
    /// </summary>
    public static Optional<T> None => default;

    /// <summary>
    /// Gets an Optional with a value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An Optional containing the value.</returns>
    public static Optional<T> Some(T value) => new(value, true);

    /// <summary>
    /// Gets the value if present, otherwise returns the default.
    /// </summary>
    /// <param name="defaultValue">The default value to return if no value is present.</param>
    /// <returns>The value or default.</returns>
    public T ValueOr(T defaultValue) => _hasValue ? _value : defaultValue;

    /// <summary>
    /// Determines whether the specified Optional is equal to the current Optional.
    /// </summary>
    /// <param name="other">The Optional to compare with the current Optional.</param>
    /// <returns>true if the specified Optional is equal to the current Optional; otherwise, false.</returns>
    public bool Equals(Optional<T> other)
    {
        if (!_hasValue && !other._hasValue)
        {
            return true;
        }

        if (_hasValue != other._hasValue)
        {
            return false;
        }

        return EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (!_hasValue)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + _hasValue.GetHashCode();
            hash = (hash * 31) + (_value?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <summary>
    /// Determines whether two specified instances of Optional are equal.
    /// </summary>
    /// <param name="left">The first Optional to compare.</param>
    /// <param name="right">The second Optional to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified instances of Optional are not equal.
    /// </summary>
    /// <param name="left">The first Optional to compare.</param>
    /// <param name="right">The second Optional to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts a value to an Optional.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Optional<T>(T value) => Some(value);
}
