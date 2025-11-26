// Port of DynamicData to R3.

namespace R3.DynamicData.Utilities;

/// <summary>
/// A mutable integer wrapper for use in closure-free state structs.
/// Allows passing by reference while maintaining struct semantics.
/// </summary>
internal sealed class RefInt
{
    /// <summary>
    /// Gets or sets the integer value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefInt"/> class with the specified value.
    /// </summary>
    /// <param name="value">The initial value.</param>
    public RefInt(int value = 0)
    {
        Value = value;
    }

    /// <summary>
    /// Increments the value by 1.
    /// </summary>
    /// <returns>The new value after incrementing.</returns>
    public int Increment() => ++Value;

    /// <summary>
    /// Decrements the value by 1.
    /// </summary>
    /// <returns>The new value after decrementing.</returns>
    public int Decrement() => --Value;

    /// <summary>
    /// Adds the specified amount to the value.
    /// </summary>
    /// <param name="amount">The amount to add.</param>
    /// <returns>The new value after adding.</returns>
    public int Add(int amount) => Value += amount;

    /// <summary>
    /// Subtracts the specified amount from the value.
    /// </summary>
    /// <param name="amount">The amount to subtract.</param>
    /// <returns>The new value after subtracting.</returns>
    public int Subtract(int amount) => Value -= amount;
}
