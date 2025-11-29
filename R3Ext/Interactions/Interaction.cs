using R3;

namespace R3Ext;

public class Interaction<TInput, TOutput> : IInteraction<TInput, TOutput>
{
    private readonly List<Func<IInteractionContext<TInput, TOutput>, Observable<Unit>>> _handlers = new(4);
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

        var wrappedHandler = new HandlerWrapper<TDontCare>(handler);
        this.AddHandler(wrappedHandler.Invoke);
        return new HandlerUnregistration<TInput, TOutput>(this, wrappedHandler.Invoke);
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

    internal void AddHandler(Func<IInteractionContext<TInput, TOutput>, Observable<Unit>> handler)
    {
        lock (_sync)
        {
            _handlers.Add(handler);
        }
    }

    internal void RemoveHandler(Func<IInteractionContext<TInput, TOutput>, Observable<Unit>> handler)
    {
        lock (_sync)
        {
            _handlers.Remove(handler);
        }
    }

    /// <summary>
    /// Wraps a handler function to convert Observable&lt;TDontCare&gt; to Observable&lt;Unit&gt;.
    /// This avoids creating a closure for each registration.
    /// </summary>
    private sealed class HandlerWrapper<TDontCare>(Func<IInteractionContext<TInput, TOutput>, Observable<TDontCare>> handler)
    {
        public Observable<Unit> Invoke(IInteractionContext<TInput, TOutput> context)
        {
            return handler(context).Select(static _ => Unit.Default);
        }
    }
}

/// <summary>
/// Disposable that removes a handler from an interaction when disposed.
/// This avoids creating a closure for Disposable.Create.
/// </summary>
internal sealed class HandlerUnregistration<TInput, TOutput>(
    Interaction<TInput, TOutput> interaction,
    Func<IInteractionContext<TInput, TOutput>, Observable<Unit>> handler) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        interaction.RemoveHandler(handler);
    }
}
