using System;
using System.Threading;
using System.Threading.Tasks;
using R3;

namespace R3Ext;

/// <summary>
/// Asynchronous operation extensions for R3 observables.
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Project values to tasks, cancelling the previous task when a new value arrives (latest only).
    /// Cancellation is propagated via CancellationToken; uses AwaitOperation.Switch.
    /// </summary>
    public static Observable<TResult> SelectLatestAsync<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return source.SelectAwait(selector, AwaitOperation.Switch, configureAwait, cancelOnCompleted);
    }

    /// <summary>
    /// SelectLatestAsync overload for Task-returning projector.
    /// </summary>
    public static Observable<TResult> SelectLatestAsync<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, Task<TResult>> selector,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return source.SelectAwait<TSource, TResult>(
            (x, ct) => new ValueTask<TResult>(selector(x)),
            AwaitOperation.Switch,
            configureAwait,
            cancelOnCompleted);
    }

    /// <summary>
    /// Project values to tasks sequentially (queue new values until the current task completes).
    /// Uses AwaitOperation.Sequential.
    /// </summary>
    public static Observable<TResult> SelectAsyncSequential<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return source.SelectAwait(selector, AwaitOperation.Sequential, configureAwait, cancelOnCompleted);
    }

    /// <summary>
    /// SelectAsyncSequential overload for Task-returning projector.
    /// </summary>
    public static Observable<TResult> SelectAsyncSequential<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, Task<TResult>> selector,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        return source.SelectAwait<TSource, TResult>((x, ct) => new ValueTask<TResult>(selector(x)), AwaitOperation.Sequential, configureAwait, cancelOnCompleted);
    }

    /// <summary>
    /// Project values concurrently up to maxConcurrency. Uses AwaitOperation.Parallel.
    /// </summary>
    public static Observable<TResult> SelectAsyncConcurrent<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        int maxConcurrency,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        if (maxConcurrency == 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        return source.SelectAwait(selector, AwaitOperation.Parallel, configureAwait, cancelOnCompleted, maxConcurrency);
    }

    /// <summary>
    /// SelectAsyncConcurrent overload for Task-returning projector.
    /// </summary>
    public static Observable<TResult> SelectAsyncConcurrent<TSource, TResult>(this Observable<TSource> source,
        Func<TSource, Task<TResult>> selector,
        int maxConcurrency,
        bool configureAwait = true,
        bool cancelOnCompleted = true)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        if (maxConcurrency == 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        return source.SelectAwait<TSource, TResult>((x, ct) => new ValueTask<TResult>(selector(x)), AwaitOperation.Parallel, configureAwait, cancelOnCompleted, maxConcurrency);
    }

    /// <summary>
    /// Subscribe asynchronously to each value with a specified await operation strategy.
    /// </summary>
    public static IDisposable SubscribeAsync<T>(this Observable<T> source,
        Func<T, CancellationToken, ValueTask> onNextAsync,
        AwaitOperation awaitOperation = AwaitOperation.Sequential,
        bool configureAwait = true,
        bool cancelOnCompleted = true,
        int maxConcurrent = -1)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (onNextAsync is null) throw new ArgumentNullException(nameof(onNextAsync));
        return source.SubscribeAwait(onNextAsync, awaitOperation, configureAwait, cancelOnCompleted, maxConcurrent);
    }

    /// <summary>
    /// SubscribeAsync overload for Task-returning handler.
    /// </summary>
    public static IDisposable SubscribeAsync<T>(this Observable<T> source,
        Func<T, Task> onNextAsync,
        AwaitOperation awaitOperation = AwaitOperation.Sequential,
        bool configureAwait = true,
        bool cancelOnCompleted = true,
        int maxConcurrent = -1)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (onNextAsync is null) throw new ArgumentNullException(nameof(onNextAsync));
        return source.SubscribeAwait((x, ct) => new ValueTask(onNextAsync(x)), awaitOperation, configureAwait, cancelOnCompleted, maxConcurrent);
    }
}
