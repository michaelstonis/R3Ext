namespace R3Ext;

/// <summary>Contains contextual information for an interaction.</summary>
/// <typeparam name="TInput">The type of the interaction's input.</typeparam>
/// <typeparam name="TOutput">The type of the interaction's output.</typeparam>
/// <remarks>
/// Instances of this interface are passed into interaction handlers. The <see cref="Input"/> property exposes
/// the input to the interaction, whilst the <see cref="SetOutput"/> method allows a handler to provide the
/// output. Handlers should call <see cref="SetOutput"/> exactly once; subsequent calls indicate a logic error and
/// will throw. Check <see cref="IsHandled"/> before invoking long-running logic if only one handler should respond.
/// </remarks>
public interface IInteractionContext<out TInput, in TOutput>
{
    /// <summary>Gets the input for the interaction.</summary>
    TInput Input { get; }

    /// <summary>Gets a value indicating whether the interaction is handled. That is, whether the output has been set.</summary>
    bool IsHandled { get; }

    /// <summary>Sets the output for the interaction.</summary>
    /// <param name="output">The output.</param>
    void SetOutput(TOutput output);
}
