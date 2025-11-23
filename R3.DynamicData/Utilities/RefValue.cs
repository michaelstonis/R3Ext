// Port of DynamicData to R3.

namespace R3.DynamicData.Utilities;

/// <summary>
/// A mutable generic value wrapper for use in closure-free state structs.
/// Allows passing by reference while maintaining struct semantics.
/// </summary>
/// <typeparam name="T">The type of value to wrap.</typeparam>
internal sealed class RefValue<T>
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefValue{T}"/> class with the default value.
    /// </summary>
    public RefValue()
    {
        Value = default!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefValue{T}"/> class with the specified value.
    /// </summary>
    /// <param name="value">The initial value.</param>
    public RefValue(T value)
    {
        Value = value;
    }
}
