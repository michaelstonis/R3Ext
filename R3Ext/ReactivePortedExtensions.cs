using System;
using System.Diagnostics.CodeAnalysis;
using R3;

namespace R3Ext;

/// <summary>
/// Extensions ported/adapted from ReactiveUI/Extensions for R3.
/// Each method is committed individually per migration requirement.
/// </summary>
public static class ReactivePortedExtensions
{
    /// <summary>
    /// Converts any upstream values to a Unit signal.
    /// Equivalent to Rx's AsSignal; maps to R3's AsUnitObservable.
    /// </summary>
    public static Observable<Unit> AsSignal<T>(this Observable<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.AsUnitObservable();
    }

    /// <summary>
    /// Logical NOT for boolean streams.
    /// </summary>
    public static Observable<bool> Not(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Select(x => !x);
    }

    /// <summary>
    /// Filters a boolean stream to only true values.
    /// </summary>
    public static Observable<bool> WhereTrue(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Where(x => x);
    }

    /// <summary>
    /// Filters a boolean stream to only false values.
    /// </summary>
    public static Observable<bool> WhereFalse(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Where(x => !x);
    }

    /// <summary>
    /// Filters out null values for nullable reference types and casts to non-nullable.
    /// </summary>
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source) where T : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Observable.Create<T>(observer =>
        {
            return source.Subscribe(
                x =>
                {
                    if (x is not null)
                    {
                        observer.OnNext(x);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Filters out null values for nullable value types and casts to non-nullable.
    /// </summary>
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source) where T : struct
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Observable.Create<T>(observer =>
        {
            return source.Subscribe(
                x =>
                {
                    if (x.HasValue)
                    {
                        observer.OnNext(x.Value);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// DebounceImmediate: emits the first item immediately, then debounces subsequent items.
    /// Equivalent to combining Take(1) with Debounce for the remainder of the stream.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="source">Upstream observable.</param>
    /// <param name="dueTime">Debounce window applied after first emission.</param>
    /// <param name="timeProvider">Optional TimeProvider; defaults to ObservableSystem.DefaultTimeProvider.</param>
    /// <returns>An observable that emits first value immediately then debounces subsequent values.</returns>
    public static Observable<T> DebounceImmediate<T>(this Observable<T> source, TimeSpan dueTime, TimeProvider? timeProvider = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (dueTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(dueTime));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        // Share subscription to avoid multiple subscriptions to source.
        var shared = source.Share();
        var first = shared.Take(1);
        var rest = shared.Skip(1).Debounce(dueTime, tp);
        return Observable.Merge(first, rest);
    }

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
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (quietPeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<HeartbeatEvent<T>>(observer =>
        {
            object gate = new();
            bool disposed = false;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null; // using ITimer from TimeProvider

            void DisposeTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            void ScheduleHeartbeat()
            {
                DisposeTimer();
                timer = tp.CreateTimer(_ =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        observer.OnNext(new HeartbeatEvent<T>(true, default));
                    }
                }, null, quietPeriod, quietPeriod);
            }

            upstream = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        observer.OnNext(new HeartbeatEvent<T>(false, x));
                        ScheduleHeartbeat();
                    }
                },
                ex =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        observer.OnErrorResume(ex);
                    }
                },
                r =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        DisposeTimer();
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                lock (gate)
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
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (quietPeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<StaleEvent<T>>(observer =>
        {
            object gate = new();
            bool disposed = false;
            bool staleEmitted = false;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(_ =>
                    {
                        lock (gate)
                        {
                            if (disposed) return;
                            if (!staleEmitted)
                            {
                                observer.OnNext(new StaleEvent<T>(true, default));
                                staleEmitted = true;
                            }
                            // Stop timer until next value arrives.
                            timer!.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                        }
                    }, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                }
            }

            void ResetQuietTimer()
            {
                EnsureTimer();
                staleEmitted = false;
                timer!.Change(quietPeriod, System.Threading.Timeout.InfiniteTimeSpan);
            }

            // Start timer immediately to detect initial inactivity.
            ResetQuietTimer();

            upstream = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        observer.OnNext(new StaleEvent<T>(false, x));
                        ResetQuietTimer();
                    }
                },
                ex =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        timer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                        observer.OnErrorResume(ex);
                    }
                },
                r =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        timer?.Dispose();
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    disposed = true;
                    timer?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// BufferUntilInactive groups values into arrays separated by periods of inactivity.
    /// When no value is received for <paramref name="quietPeriod"/>, the current buffer is emitted.
    /// </summary>
    public static Observable<T[]> BufferUntilInactive<T>(this Observable<T> source, TimeSpan quietPeriod, TimeProvider? timeProvider = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (quietPeriod <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(quietPeriod));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<T[]>(observer =>
        {
            object gate = new();
            bool disposed = false;
            var buffer = new System.Collections.Generic.List<T>();
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(_ =>
                    {
                        T[]? toEmit = null;
                        lock (gate)
                        {
                            if (disposed) return;
                            if (buffer.Count > 0)
                            {
                                toEmit = buffer.ToArray();
                                buffer.Clear();
                            }
                            // Stop timer after firing; restarted on next value.
                            timer!.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                        }
                        if (toEmit is not null)
                        {
                            observer.OnNext(toEmit);
                        }
                    }, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                }
            }

            void ResetQuietTimer()
            {
                EnsureTimer();
                timer!.Change(quietPeriod, System.Threading.Timeout.InfiniteTimeSpan);
            }

            upstream = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        buffer.Add(x);
                        ResetQuietTimer();
                    }
                },
                ex =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
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
                    lock (gate)
                    {
                        if (disposed) return;
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
                lock (gate)
                {
                    if (disposed) return;
                    disposed = true;
                    timer?.Dispose();
                    upstream?.Dispose();
                    buffer.Clear();
                }
            });
        });
    }

    /// <summary>
    /// Conflate enforces a minimum spacing between emissions while always outputting the most recent value.
    /// Emits immediately on the first value, then at most once per <paramref name="period"/> thereafter,
    /// emitting the latest seen value if any arrived during the window.
    /// </summary>
    public static Observable<T> Conflate<T>(this Observable<T> source, TimeSpan period, TimeProvider? timeProvider = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<T>(observer =>
        {
            object gate = new();
            bool disposed = false;
            bool gating = false;
            bool hasPending = false;
            T? latest = default;
            IDisposable? upstream = null;
            System.Threading.ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(_ =>
                    {
                        T? toEmit = default;
                        bool emit = false;
                        lock (gate)
                        {
                            if (disposed) return;
                            if (hasPending)
                            {
                                toEmit = latest;
                                hasPending = false;
                                emit = true;
                                // continue gating, schedule next window
                                timer!.Change(period, System.Threading.Timeout.InfiniteTimeSpan);
                            }
                            else
                            {
                                // no pending, stop gating
                                gating = false;
                                timer!.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                            }
                        }
                        if (emit)
                        {
                            observer.OnNext(toEmit!);
                        }
                    }, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        if (!gating)
                        {
                            // First value in a new window: emit immediately and start window
                            observer.OnNext(x);
                            gating = true;
                            hasPending = false;
                            latest = default;
                            EnsureTimer();
                            timer!.Change(period, System.Threading.Timeout.InfiniteTimeSpan);
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
                    lock (gate)
                    {
                        if (disposed) return;
                        timer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                        observer.OnErrorResume(ex);
                    }
                },
                r =>
                {
                    lock (gate)
                    {
                        if (disposed) return;
                        timer?.Dispose();
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    if (disposed) return;
                    disposed = true;
                    timer?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }
}
