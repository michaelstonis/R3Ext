// Port of DynamicData Error to R3.
namespace R3.DynamicData.Kernel;

/// <summary>
/// Container for an error that occurred during transformation with the source item and key.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public sealed class Error<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Error{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="value">The source value being transformed.</param>
    /// <param name="key">The key of the source item.</param>
    public Error(Exception exception, TObject value, TKey key)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// Gets the exception that occurred during transformation.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the source value that caused the error.
    /// </summary>
    public TObject Value { get; }

    /// <summary>
    /// Gets the key of the source item.
    /// </summary>
    public TKey Key { get; }

    /// <inheritdoc/>
    public override string ToString() => $"Error transforming {Value} with key {Key}: {Exception.Message}";
}
