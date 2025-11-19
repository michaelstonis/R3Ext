namespace R3Ext;

public interface IOutputContext<out TInput, TOutput> : IInteractionContext<TInput, TOutput>
{
    TOutput GetOutput();
}
