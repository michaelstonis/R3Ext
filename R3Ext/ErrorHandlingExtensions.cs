using System;
using System.Threading;
using R3;

namespace R3Ext;

/// <summary>
/// Error handling and retry extensions for R3 observables.
/// </summary>
public static class ErrorHandlingExtensions
{
    /// <summary>
    /// Ignore errors from the source and complete instead.
    /// </summary>
    public static Observable<T> CatchIgnore<T>(this Observable<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source
            .OnErrorResumeAsFailure()
            .Catch(Observable.Empty<T>());
    }

    /// <summary>
    /// Replace an error with a single fallback value, then complete.
    /// </summary>
    public static Observable<T> CatchAndReturn<T>(this Observable<T> source, T value)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source
            .OnErrorResumeAsFailure()
            .Catch(Observable.Return(value));
    }

    /// <summary>
    /// Retry on failure completion (converted from OnErrorResume) a specified number of times.
    /// Use negative retryCount for infinite retries. Optional delay between retries.
    /// </summary>
    public static Observable<T> OnErrorRetry<T>(this Observable<T> source, int retryCount = -1, TimeSpan? delay = null, TimeProvider? timeProvider = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int attempts = 0;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void DisposeTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            void SubscribeOnce()
            {
                upstream = source.OnErrorResumeAsFailure().Subscribe(
                    x =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            if (r.IsFailure)
                            {
                                if (retryCount < 0 || attempts++ < retryCount)
                                {
                                    if (delay.HasValue)
                                    {
                                        DisposeTimer();
                                        timer = tp.CreateTimer(_ =>
                                        {
                                            using (gate.EnterScope())
                                            {
                                                if (disposed) return;
                                                SubscribeOnce();
                                            }
                                        }, null, delay.Value, System.Threading.Timeout.InfiniteTimeSpan);
                                    }
                                    else
                                    {
                                        SubscribeOnce();
                                    }
                                }
                                else
                                {
                                    observer.OnCompleted(r);
                                }
                            }
                            else
                            {
                                observer.OnCompleted(r);
                            }
                        }
                    });
            }

            SubscribeOnce();

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    disposed = true;
                    DisposeTimer();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Retry on specific exception type. Optional per-error callback, retry count, and delay.
    /// </summary>
    public static Observable<T> OnErrorRetry<T, TException>(this Observable<T> source, Action<TException>? onError = null, int retryCount = -1, TimeSpan? delay = null, TimeProvider? timeProvider = null) where TException : Exception
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int attempts = 0;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void DisposeTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            void SubscribeOnce()
            {
                upstream = source.OnErrorResumeAsFailure().Subscribe(
                    x =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            if (r.IsFailure)
                            {
                                var ex = r.Exception as TException;
                                if (ex is not null)
                                {
                                    onError?.Invoke(ex);
                                    if (retryCount < 0 || attempts++ < retryCount)
                                    {
                                        if (delay.HasValue)
                                        {
                                            DisposeTimer();
                                            timer = tp.CreateTimer(_ =>
                                            {
                                                using (gate.EnterScope())
                                                {
                                                    if (disposed) return;
                                                    SubscribeOnce();
                                                }
                                            }, null, delay.Value, System.Threading.Timeout.InfiniteTimeSpan);
                                        }
                                        else
                                        {
                                            SubscribeOnce();
                                        }
                                    }
                                    else
                                    {
                                        observer.OnCompleted(r);
                                    }
                                }
                                else
                                {
                                    observer.OnCompleted(r);
                                }
                            }
                            else
                            {
                                observer.OnCompleted(r);
                            }
                        }
                    });
            }

            SubscribeOnce();

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    disposed = true;
                    DisposeTimer();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Retry with exponential backoff on failure completion. Stops after maxRetries (inclusive) failures.
    /// </summary>
    public static Observable<T> RetryWithBackoff<T>(this Observable<T> source, int maxRetries, TimeSpan initialDelay, double factor = 2.0, TimeSpan? maxDelay = null, TimeProvider? timeProvider = null, Action<Exception>? onError = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (initialDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialDelay));
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int attempts = 0;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void DisposeTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            TimeSpan ComputeDelay(int attempt)
            {
                try
                {
                    var d = TimeSpan.FromTicks((long)(initialDelay.Ticks * Math.Pow(factor, attempt)));
                    if (maxDelay.HasValue && d > maxDelay.Value) d = maxDelay.Value;
                    return d;
                }
                catch
                {
                    return maxDelay ?? initialDelay;
                }
            }

            void SubscribeOnce()
            {
                upstream = source.OnErrorResumeAsFailure().Subscribe(
                    x =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed) return;
                            if (r.IsFailure)
                            {
                                onError?.Invoke(r.Exception!);
                                if (attempts < maxRetries)
                                {
                                    var nextDelay = ComputeDelay(attempts);
                                    attempts++;
                                    DisposeTimer();
                                    timer = tp.CreateTimer(_ =>
                                    {
                                        using (gate.EnterScope())
                                        {
                                            if (disposed) return;
                                            SubscribeOnce();
                                        }
                                    }, null, nextDelay, System.Threading.Timeout.InfiniteTimeSpan);
                                }
                                else
                                {
                                    observer.OnCompleted(r);
                                }
                            }
                            else
                            {
                                observer.OnCompleted(r);
                            }
                        }
                    });
            }

            SubscribeOnce();

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    disposed = true;
                    DisposeTimer();
                    upstream?.Dispose();
                }
            });
        });
    }
}
