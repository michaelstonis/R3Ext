using R3;

namespace R3Ext;

public class Interaction<TInput, TOutput> : IInteraction<TInput, TOutput>
{
    private readonly List<Func<IInteractionContext<TInput, TOutput>, Observable<Unit>>> _handlers = new();
    private readonly object _sync = new();

    public IDisposable RegisterHandler(Action<IInteractionContext<TInput, TOutput>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return this.RegisterHandler(ctx =>
        {
            handler(ctx);
            return Observable.ReturnUnit();
        });
    }

    public IDisposable RegisterHandler(Func<IInteractionContext<TInput, TOutput>, Task> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return this.RegisterHandler(ctx => Observable.FromAsync(async ct => { await handler(ctx); }));
    }

    public IDisposable RegisterHandler<TDontCare>(Func<IInteractionContext<TInput, TOutput>, Observable<TDontCare>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Observable<Unit> Wrapper(IInteractionContext<TInput, TOutput> context)
        {
            return handler(context).Select(_ => Unit.Default);
        }

        this.AddHandler(Wrapper);
        return Disposable.Create(() => this.RemoveHandler(Wrapper));
    }

    public IDisposable RegisterHandler<TDontCare>(Func<IInteractionContext<TInput, TOutput>, IObservable<TDontCare>> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return this.RegisterHandler(ctx => handler(ctx).ToObservable());
    }

    public virtual Observable<TOutput> Handle(TInput input)
    {
        IOutputContext<TInput, TOutput> context = this.GenerateContext(input);

        return Enumerable.Reverse(this.GetHandlers())
            .ToObservable()
            .Select(handler => Observable.Defer(() => handler(context)))
            .Concat()
            .TakeWhile(_ => !context.IsHandled)
            .IgnoreElements()
            .Select(_ => default(TOutput)!)
            .Concat(
                Observable.Defer(() => context.IsHandled
                    ? Observable.Return(context.GetOutput())
                    : Observable.Throw<TOutput>(new UnhandledInteractionException<TInput, TOutput>(this, input))));
    }

    protected Func<IInteractionContext<TInput, TOutput>, Observable<Unit>>[] GetHandlers()
    {
        lock (_sync)
        {
            return _handlers.ToArray();
        }
    }

    protected virtual IOutputContext<TInput, TOutput> GenerateContext(TInput input)
    {
        return new InteractionContext<TInput, TOutput>(input);
    }

    private void AddHandler(Func<IInteractionContext<TInput, TOutput>, Observable<Unit>> handler)
    {
        lock (_sync)
        {
            _handlers.Add(handler);
        }
    }

    private void RemoveHandler(Func<IInteractionContext<TInput, TOutput>, Observable<Unit>> handler)
    {
        lock (_sync)
        {
            _handlers.Remove(handler);
        }
    }
}
