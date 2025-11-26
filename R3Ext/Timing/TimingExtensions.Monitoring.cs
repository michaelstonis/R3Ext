using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
    /// <summary>
    /// Wrapper emitted by Heartbeat indicating either a data value or a heartbeat placeholder.
    /// </summary>
    public readonly struct HeartbeatEvent<T>
    {
        public bool IsHeartbeat { get; }

        public T? Value { get; }

        internal HeartbeatEvent(bool isHeartbeat, T? value)
        {
            IsHeartbeat = isHeartbeat;
            Value = value;
        }
    }

    /// <summary>
    /// Emits original values wrapped in Heartbeat along with heartbeat markers during periods of inactivity.
    /// Heartbeats begin after <paramref name="quietPeriod"/> since the last value and repeat until a new value arrives.
    /// </summary>
    public static Observable<HeartbeatEvent<T>> Heartbeat<T>(this Observable<T> source, TimeSpan quietPeriod, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (quietPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<HeartbeatEvent<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? upstream = null;
            ITimer? timer = null; // using ITimer from TimeProvider

            void DisposeTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            void ScheduleHeartbeat()
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

                            observer.OnNext(new HeartbeatEvent<T>(true, default));
                        }
                    }, null, quietPeriod, quietPeriod);
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

                        observer.OnNext(new HeartbeatEvent<T>(false, x));
                        ScheduleHeartbeat();
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

                        DisposeTimer();
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
                    DisposeTimer();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Wrapper emitted by DetectStale indicating either a fresh value or a stale marker when inactivity is detected.
    /// </summary>
    public readonly struct StaleEvent<T>
    {
        public bool IsStale { get; }

        public T? Value { get; }

        internal StaleEvent(bool isStale, T? value)
        {
            IsStale = isStale;
            Value = value;
        }
    }

    /// <summary>
    /// DetectStale emits a single stale marker (IsStale = true) after <paramref name="quietPeriod"/> of inactivity.
    /// Fresh values are wrapped with IsStale = false. After emitting a stale marker, no further stale markers are
    /// produced until a new value arrives and the quiet timer resets.
    /// </summary>
    public static Observable<StaleEvent<T>> DetectStale<T>(this Observable<T> source, TimeSpan quietPeriod, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (quietPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<StaleEvent<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool staleEmitted = false;
            IDisposable? upstream = null;
            ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(
                        _ =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                if (!staleEmitted)
                                {
                                    observer.OnNext(new StaleEvent<T>(true, default));
                                    staleEmitted = true;
                                }

                                // Stop timer until next value arrives.
                                timer!.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                            }
                        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            void ResetQuietTimer()
            {
                EnsureTimer();
                staleEmitted = false;
                timer!.Change(quietPeriod, Timeout.InfiniteTimeSpan);
            }

            // Start timer immediately to detect initial inactivity.
            ResetQuietTimer();

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        observer.OnNext(new StaleEvent<T>(false, x));
                        ResetQuietTimer();
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
