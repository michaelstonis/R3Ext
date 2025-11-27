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

        return this.RegisterHandler(ctx => Observable.FromAsync(_ => new ValueTask(handler(ctx))));
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
        var context = (InteractionContext<TInput, TOutput>)this.GenerateContext(input);

        // Execute handlers in LIFO order. If already handled, skip remaining.
        var handlers = Enumerable.Reverse(this.GetHandlers()).Select(handler => Observable.Defer(() =>
        {
            if (context.IsHandled)
            {
                return Observable.Empty<Unit>();
            }

            try
            {
                return handler(context);
            }
            catch (Exception ex)
            {
                return Observable.Throw<Unit>(ex);
            }
        }));

        var handlerSequence = Observable.Concat(handlers)
            .OnErrorResumeAsFailure()
            .IgnoreElements();

        Observable<TOutput> EmitResult()
        {
            if (context.IsHandled)
            {
                return Observable.Return(context.GetOutput());
            }

            return Observable.Throw<TOutput>(new UnhandledInteractionException<TInput, TOutput>(this, input));
        }

        return handlerSequence
            .Concat(Observable.ReturnUnit())
            .SelectMany(_ => Observable.Defer(EmitResult));
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
