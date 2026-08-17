namespace R3Ext;

/// <summary>Gives the ability to get the output of an interaction.</summary>
/// <typeparam name="TInput">The input type of the interaction.</typeparam>
/// <typeparam name="TOutput">The output type of the interaction.</typeparam>
public interface IOutputContext<out TInput, TOutput> : IInteractionContext<TInput, TOutput>
{
    /// <summary>Gets the output of the interaction.</summary>
    /// <returns>The output value set by the handler.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the output has not been set.</exception>
    TOutput GetOutput();
}
