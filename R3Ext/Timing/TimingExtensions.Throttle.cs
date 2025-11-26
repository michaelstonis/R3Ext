using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
    /// <summary>
    /// Conflate enforces a minimum spacing between emissions while always outputting the most recent value.
    /// Emits immediately on the first value, then at most once per <paramref name="period"/> thereafter,
    /// emitting the latest seen value if any arrived during the window.
    /// </summary>
    public static Observable<T> Conflate<T>(this Observable<T> source, TimeSpan period, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool gating = false;
            bool hasPending = false;
            T? latest = default;
            IDisposable? upstream = null;
            ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(
                        _ =>
                        {
                            T? toEmit = default;
                            bool emit = false;
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                if (hasPending)
                                {
                                    toEmit = latest;
                                    hasPending = false;
                                    emit = true;

                                    // continue gating, schedule next window
                                    timer!.Change(period, Timeout.InfiniteTimeSpan);
                                }
                                else
                                {
                                    // no pending, stop gating
                                    gating = false;
                                    timer!.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                }
                            }

                            if (emit)
                            {
                                observer.OnNext(toEmit!);
                            }
                        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (!gating)
                        {
                            // First value in a new window: emit immediately and start window
                            observer.OnNext(x);
                            gating = true;
                            hasPending = false;
                            latest = default;
                            EnsureTimer();
                            timer!.Change(period, Timeout.InfiniteTimeSpan);
                        }
                        else
                        {
                            // Within gate: store as pending latest
                            latest = x;
                            hasPending = true;
                        }
                    }
                },
                ex =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                        observer.OnErrorResume(ex);
                    }
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Dispose();
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    timer?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }
}
