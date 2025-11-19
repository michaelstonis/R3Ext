using R3;

namespace R3Ext;

/// <summary>
/// Compatibility facade preserving original static invocation style for migrated extension groups.
/// </summary>
public static class ReactivePortedExtensions
{
    public static Observable<T> FromArray<T>(params T[] items)
    {
        return CreationExtensions.FromArray(items);
    }

    public static Observable<Unit> Start(Action action, bool configureAwait = true)
    {
        return CreationExtensions.Start(action, configureAwait);
    }

    public static Observable<TResult> Start<TResult>(Func<TResult> func, bool configureAwait = true)
    {
        return CreationExtensions.Start(func, configureAwait);
    }

    public static Observable<TResult> Using<TResource, TResult>(Func<TResource> resourceFactory, Func<TResource, Observable<TResult>> observableFactory)
        where TResource : IDisposable
    {
        return CreationExtensions.Using(resourceFactory, observableFactory);
    }

    public static Observable<Unit> AsSignal<T>(Observable<T> source)
    {
        return SignalExtensions.AsSignal(source);
    }

    public static Observable<bool> Not(Observable<bool> source)
    {
        return FilteringExtensions.Not(source);
    }

    public static Observable<bool> WhereTrue(Observable<bool> source)
    {
        return FilteringExtensions.WhereTrue(source);
    }

    public static Observable<bool> WhereFalse(Observable<bool> source)
    {
        return FilteringExtensions.WhereFalse(source);
    }

    public static Observable<T> While<T>(Observable<T> source, Func<bool> condition)
    {
        return FilteringExtensions.While(source, condition);
    }
}
