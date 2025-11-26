using R3;

namespace R3Ext;

public static partial class ErrorHandlingExtensions
{
    /// <summary>
    /// Retry on failure completion (converted from OnErrorResume) a specified number of times.
    /// Use negative retryCount for infinite retries. Optional delay between retries.
    /// </summary>
    public static Observable<T> OnErrorRetry<T>(this Observable<T> source, int retryCount = -1, TimeSpan? delay = null, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int attempts = 0;
            IDisposable? upstream = null;
            ITimer? timer = null;

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
                            if (disposed)
                            {
                                return;
                            }

                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            if (r.IsFailure)
                            {
                                if (retryCount < 0 || attempts++ < retryCount)
                                {
                                    if (delay.HasValue)
                                    {
                                        DisposeTimer();
                                        timer = tp.CreateTimer(
                                            _ =>
                                            {
                                                using (gate.EnterScope())
                                                {
                                                    if (disposed)
                                                    {
                                                        return;
                                                    }

                                                    SubscribeOnce();
                                                }
                                            }, null, delay.Value, Timeout.InfiniteTimeSpan);
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
                    if (disposed)
                    {
                        return;
                    }

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
    public static Observable<T> OnErrorRetry<T, TException>(this Observable<T> source, Action<TException>? onError = null, int retryCount = -1,
        TimeSpan? delay = null, TimeProvider? timeProvider = null)
        where TException : Exception
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int attempts = 0;
            IDisposable? upstream = null;
            ITimer? timer = null;

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
                            if (disposed)
                            {
                                return;
                            }

                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            if (r.IsFailure)
                            {
                                TException? ex = r.Exception as TException;
                                if (ex is not null)
                                {
                                    onError?.Invoke(ex);
                                    if (retryCount < 0 || attempts++ < retryCount)
                                    {
                                        if (delay.HasValue)
                                        {
                                            DisposeTimer();
                                            timer = tp.CreateTimer(
                                                _ =>
                                                {
                                                    using (gate.EnterScope())
                                                    {
                                                        if (disposed)
                                                        {
                                                            return;
                                                        }

                                                        SubscribeOnce();
                                                    }
                                                }, null, delay.Value, Timeout.InfiniteTimeSpan);
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
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    DisposeTimer();
                    upstream?.Dispose();
                }
            });
        });
    }
}
