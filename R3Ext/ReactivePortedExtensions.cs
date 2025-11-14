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

    /// <summary>
    /// Returns true when all latest values are true across the provided boolean observables.
    /// </summary>
    public static Observable<bool> CombineLatestValuesAreAllTrue(this System.Collections.Generic.IEnumerable<Observable<bool>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var list = sources as System.Collections.Generic.IList<Observable<bool>> ?? new System.Collections.Generic.List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            var all = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i]) { all = false; break; }
            }
            return all;
        });
    }

    /// <summary>
    /// Returns true when all latest values are false across the provided boolean observables.
    /// </summary>
    public static Observable<bool> CombineLatestValuesAreAllFalse(this System.Collections.Generic.IEnumerable<Observable<bool>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var list = sources as System.Collections.Generic.IList<Observable<bool>> ?? new System.Collections.Generic.List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            var allFalse = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i]) { allFalse = false; break; }
            }
            return allFalse;
        });
    }

    /// <summary>
    /// Partition a stream into two by predicate: matching and non-matching.
    /// </summary>
    public static (Observable<T> True, Observable<T> False) Partition<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        var t = source.Where(predicate);
        var f = source.Where(x => !predicate(x));
        return (t, f);
    }

    /// <summary>
    /// Invoke action on subscription using R3's Do(onSubscribe:).
    /// </summary>
    public static Observable<T> DoOnSubscribe<T>(this Observable<T> source, Action onSubscribe)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (onSubscribe is null) throw new ArgumentNullException(nameof(onSubscribe));
        return source.Do(onSubscribe: onSubscribe);
    }

    /// <summary>
    /// Invoke action on dispose using R3's Do(onDispose:).
    /// </summary>
    public static Observable<T> DoOnDispose<T>(this Observable<T> source, Action onDispose)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (onDispose is null) throw new ArgumentNullException(nameof(onDispose));
        return source.Do(onDispose: onDispose);
    }

    /// <summary>
    /// Emit the first value matching the predicate, then complete.
    /// </summary>
    public static Observable<T> WaitUntil<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return source.Where(predicate).Take(1);
    }

    /// <summary>
    /// Take values until predicate matches, including the matching value, then complete.
    /// </summary>
    public static Observable<T> TakeUntil<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return Observable.Create<T>(observer =>
        {
            return source.Subscribe(
                x =>
                {
                    observer.OnNext(x);
                    if (predicate(x))
                    {
                        observer.OnCompleted();
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

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
            object gate = new();
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
                        lock (gate)
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        lock (gate)
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
                                            lock (gate)
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
    /// Retry on specific exception type. Optional per-error callback, retry count, and delay.
    /// </summary>
    public static Observable<T> OnErrorRetry<T, TException>(this Observable<T> source, Action<TException>? onError = null, int retryCount = -1, TimeSpan? delay = null, TimeProvider? timeProvider = null) where TException : Exception
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return Observable.Create<T>(observer =>
        {
            object gate = new();
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
                        lock (gate)
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        lock (gate)
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
                                                lock (gate)
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
            object gate = new();
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
                        lock (gate)
                        {
                            if (disposed) return;
                            observer.OnNext(x);
                        }
                    },
                    observer.OnErrorResume,
                    r =>
                    {
                        lock (gate)
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
                                        lock (gate)
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
}
