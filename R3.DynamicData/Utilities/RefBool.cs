// Port of DynamicData to R3.

namespace R3.DynamicData.Utilities;

/// <summary>
/// A mutable boolean wrapper for use in closure-free state structs.
/// Allows passing by reference while maintaining struct semantics.
/// </summary>
internal sealed class RefBool
{
    /// <summary>
    /// Gets or sets a value indicating whether the boolean value is true.
    /// </summary>
    public bool Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefBool"/> class with the specified value.
    /// </summary>
    /// <param name="value">The initial value.</param>
    public RefBool(bool value = false)
    {
        Value = value;
    }

    /// <summary>
    /// Toggles the value (true becomes false, false becomes true).
    /// </summary>
    /// <returns>The new value after toggling.</returns>
    public bool Toggle() => Value = !Value;

    /// <summary>
    /// Sets the value to true.
    /// </summary>
    public void SetTrue() => Value = true;

    /// <summary>
    /// Sets the value to false.
    /// </summary>
    public void SetFalse() => Value = false;
}
