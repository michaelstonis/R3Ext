using R3;

namespace R3Ext;

public interface IInteraction<TInput, TOutput>
{
    IDisposable RegisterHandler(Action<IInteractionContext<TInput, TOutput>> handler);

    IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler);

    IDisposable RegisterHandler<TDontCare>(Func<IInteractionContext<TInput, TOutput>, Observable<TDontCare>> handler);

    IDisposable RegisterHandler<TDontCare>(Func<IInteractionContext<TInput, TOutput>, IObservable<TDontCare>> handler);

    Observable<TOutput> Handle(TInput input);
}
