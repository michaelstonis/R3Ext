using R3;

namespace R3Ext;

public static partial class ErrorHandlingExtensions
{
    /// <summary>
    /// Retry with exponential backoff on failure completion. Stops after maxRetries (inclusive) failures.
    /// </summary>
    public static Observable<T> RetryWithBackoff<T>(this Observable<T> source, int maxRetries, TimeSpan initialDelay, double factor = 2.0,
        TimeSpan? maxDelay = null, TimeProvider? timeProvider = null, Action<Exception>? onError = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        if (initialDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay));
        }

        if (factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
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

            TimeSpan ComputeDelay(int attempt)
            {
                try
                {
                    TimeSpan d = TimeSpan.FromTicks((long)(initialDelay.Ticks * Math.Pow(factor, attempt)));
                    if (maxDelay.HasValue && d > maxDelay.Value)
                    {
                        d = maxDelay.Value;
                    }

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
                                onError?.Invoke(r.Exception!);
                                if (attempts < maxRetries)
                                {
                                    TimeSpan nextDelay = ComputeDelay(attempts);
                                    attempts++;
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
                                        }, null, nextDelay, Timeout.InfiniteTimeSpan);
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
