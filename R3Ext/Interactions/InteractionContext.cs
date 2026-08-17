namespace R3Ext;

/// <summary>Contains contextual information for an interaction.</summary>
/// <typeparam name="TInput">The type of the interaction's input.</typeparam>
/// <typeparam name="TOutput">The type of the interaction's output.</typeparam>
/// <remarks>
/// <para>
/// Instances of this class are passed into interaction handlers. The <see cref="Input"/> property exposes
/// the input to the interaction, whilst the <see cref="SetOutput"/> method allows a handler to provide the
/// output.
/// </para>
/// <para>
/// Calling <see cref="SetOutput"/> more than once throws an <see cref="InvalidOperationException"/>, ensuring the
/// handler's reply remains deterministic even when multiple handlers run concurrently. Use <see cref="IsHandled"/>
/// to guard logic that should only execute once.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Input = {Input}, IsHandled = {IsHandled}")]
public sealed class InteractionContext<TInput, TOutput> : IOutputContext<TInput, TOutput>
{
    private TOutput _output = default!;
    private int _outputSet;

    /// <summary>Initializes a new instance of the <see cref="InteractionContext{TInput, TOutput}"/> class with the specified input.</summary>
    /// <param name="input">The input for the interaction.</param>
    public InteractionContext(TInput input)
    {
        Input = input;
    }

    /// <inheritdoc />
    public TInput Input { get; }

    /// <inheritdoc />
    public bool IsHandled => Volatile.Read(ref _outputSet) == 1;

    /// <inheritdoc />
    public void SetOutput(TOutput output)
    {
        if (Interlocked.CompareExchange(ref _outputSet, 1, 0) != 0)
        {
            throw new InvalidOperationException("Output has already been set.");
        }

        _output = output;
    }

    /// <inheritdoc />
    public TOutput GetOutput()
    {
        if (Volatile.Read(ref _outputSet) == 0)
        {
            throw new InvalidOperationException("Output has not been set.");
        }

        return _output;
    }
}
