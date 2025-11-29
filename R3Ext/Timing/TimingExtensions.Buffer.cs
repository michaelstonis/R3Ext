using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
    /// <summary>
    /// BufferUntilInactive groups values into arrays separated by periods of inactivity.
    /// When no value is received for <paramref name="quietPeriod"/>, the current buffer is emitted.
    /// </summary>
    public static Observable<T[]> BufferUntilInactive<T>(this Observable<T> source, TimeSpan quietPeriod, TimeProvider? timeProvider = null)
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

        return Observable.Create<T[]>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            List<T> buffer = new(16); // Initial capacity to reduce early resizes
            IDisposable? upstream = null;
            ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(
                        _ =>
                        {
                            T[]? toEmit = null;
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                if (buffer.Count > 0)
                                {
                                    toEmit = buffer.ToArray();
                                    buffer.Clear();
                                }

                                // Stop timer after firing; restarted on next value.
                                timer!.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                            }

                            if (toEmit is not null)
                            {
                                observer.OnNext(toEmit);
                            }
                        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            void ResetQuietTimer()
            {
                EnsureTimer();
                timer!.Change(quietPeriod, Timeout.InfiniteTimeSpan);
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

                        buffer.Add(x);
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

                        // Flush any pending on error then propagate
                        if (buffer.Count > 0)
                        {
                            observer.OnNext(buffer.ToArray());
                            buffer.Clear();
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

                        timer?.Dispose();
                        if (buffer.Count > 0)
                        {
                            observer.OnNext(buffer.ToArray());
                            buffer.Clear();
                        }

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
                    buffer.Clear();
                }
            });
        });
    }
}
