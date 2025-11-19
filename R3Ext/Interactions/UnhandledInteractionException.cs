namespace R3Ext;

public class UnhandledInteractionException<TInput, TOutput> : Exception
{
    public UnhandledInteractionException(IInteraction<TInput, TOutput> interaction, TInput input)
        : base("Failed to find a registration for an Interaction.")
    {
        Interaction = interaction;
        Input = input;
    }

    public UnhandledInteractionException()
    {
    }

    public UnhandledInteractionException(string message)
        : base(message)
    {
    }

    public UnhandledInteractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IInteraction<TInput, TOutput>? Interaction { get; }

    public TInput Input { get; } = default!;
}
