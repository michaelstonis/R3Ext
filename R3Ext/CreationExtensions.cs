using R3;

namespace R3Ext;

/// <summary>
/// Observable creation extensions for R3.
/// </summary>
public static class CreationExtensions
{
    /// <summary>
    /// Create an observable sequence from provided items, emitting synchronously then completing.
    /// </summary>
    public static Observable<T> FromArray<T>(params T[] items)
    {
        items ??= Array.Empty<T>();
        return Observable.Create<T>(observer =>
        {
            for (int i = 0; i < items.Length; i++)
            {
                observer.OnNext(items[i]);
            }

            observer.OnCompleted();
            return Disposable.Create(() => { });
        });
    }

    /// <summary>
    /// Creates an observable sequence that depends on a resource object, disposing it when the sequence terminates.
    /// </summary>
    public static Observable<TResult> Using<TResource, TResult>(Func<TResource> resourceFactory, Func<TResource, Observable<TResult>> observableFactory)
        where TResource : IDisposable
    {
        if (resourceFactory is null)
        {
            throw new ArgumentNullException(nameof(resourceFactory));
        }

        if (observableFactory is null)
        {
            throw new ArgumentNullException(nameof(observableFactory));
        }

        return Observable.Create<TResult>(observer =>
        {
            TResource resource = resourceFactory();
            IDisposable subscription = observableFactory(resource).Subscribe(observer);
            return Disposable.Combine(subscription, resource);
        });
    }

    /// <summary>
    /// Starts an action asynchronously and emits Unit on completion.
    /// </summary>
    public static Observable<Unit> Start(Action action, bool configureAwait = true)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return Observable.FromAsync(
            async ct =>
            {
                action();
                await ValueTask.CompletedTask;
            }, configureAwait);
    }

    /// <summary>
    /// Starts a function asynchronously and emits its result, then completes.
    /// </summary>
    public static Observable<TResult> Start<TResult>(Func<TResult> func, bool configureAwait = true)
    {
        if (func is null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        return Observable.FromAsync(ct => new ValueTask<TResult>(func()), configureAwait);
    }
}
