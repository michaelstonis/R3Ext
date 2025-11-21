using System;
// Deprecated: original monolithic extension container replaced by split logical files.
// Intentionally left blank.
{
    public static Observable<T> FromArray<T>(params T[] items) => CreationExtensions.FromArray(items);
public static Observable<Unit> Start(Action action, bool configureAwait = true) => CreationExtensions.Start(action, configureAwait);
public static Observable<TResult> Start<TResult>(Func<TResult> func, bool configureAwait = true) => CreationExtensions.Start(func, configureAwait);
public static Observable<TResult> Using<TResource, TResult>(Func<TResource> resourceFactory, Func<TResource, Observable<TResult>> observableFactory) where TResource : IDisposable => CreationExtensions.Using(resourceFactory, observableFactory);
public static Observable<Unit> AsSignal<T>(Observable<T> source) => SignalExtensions.AsSignal(source);
public static Observable<bool> Not(Observable<bool> source) => FilteringExtensions.Not(source);
public static Observable<bool> WhereTrue(Observable<bool> source) => FilteringExtensions.WhereTrue(source);
public static Observable<bool> WhereFalse(Observable<bool> source) => FilteringExtensions.WhereFalse(source);
public static Observable<T> While<T>(Observable<T> source, Func<bool> condition) => FilteringExtensions.While(source, condition);
}
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

/// <summary>
/// Create an observable sequence from provided items, emitting synchronously then completing.
/// </summary>
public static Observable<T> FromArray<T>(params T[] items)
{
    return Observable.Create<T, T[]>(items, static (observer, state) =>
    {
        for (int i = 0; i < state.Length; i++)
        {
            observer.OnNext(state[i]);
        }
        observer.OnCompleted();
        return Disposable.Empty;
    });
}

/// <summary>
/// Expand sequences emitted by the source into individual items.
/// Optimized for arrays and IList to avoid iterator allocations.
/// </summary>
public static Observable<T> ForEach<T, TEnumerable>(this Observable<TEnumerable> source)
    where TEnumerable : System.Collections.Generic.IEnumerable<T>
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    return Observable.Create<T, Observable<TEnumerable>>(source, static (observer, state) =>
    {
        return state.Subscribe(seq =>
        {
            if (seq is null) return;
            // Fast path for arrays
            if (seq is T[] arr)
            {
                for (int i = 0; i < arr.Length; i++) observer.OnNext(arr[i]);
                return;
            }
            // Fast path for IList
            if (seq is System.Collections.Generic.IList<T> list)
            {
                for (int i = 0; i < list.Count; i++) observer.OnNext(list[i]);
                return;
            }
            foreach (var item in seq)
            {
                observer.OnNext(item);
            }
        },
        observer.OnErrorResume,
        observer.OnCompleted);
    });
}

/// <summary>
/// Expand array items emitted by the source into individual items.
/// </summary>
public static Observable<T> ForEach<T>(this Observable<T[]> source)
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    return Observable.Create<T, Observable<T[]>>(source, static (observer, state) =>
    {
        return state.Subscribe(arr =>
        {
            if (arr is null) return;
            for (int i = 0; i < arr.Length; i++) observer.OnNext(arr[i]);
        },
        observer.OnErrorResume,
        observer.OnCompleted);
    });
}

/// <summary>
/// Expand IList items emitted by the source into individual items.
/// </summary>
public static Observable<T> ForEach<T>(this Observable<System.Collections.Generic.IList<T>> source)
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    return Observable.Create<T, Observable<System.Collections.Generic.IList<T>>>(source, static (observer, state) =>
    {
        return state.Subscribe(list =>
        {
            if (list is null) return;
            for (int i = 0; i < list.Count; i++) observer.OnNext(list[i]);
        },
        observer.OnErrorResume,
        observer.OnCompleted);
    });
}

/// <summary>
/// Expand List items emitted by the source into individual items.
/// </summary>
public static Observable<T> ForEach<T>(this Observable<System.Collections.Generic.List<T>> source)
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    return Observable.Create<T>(observer =>
    {
        return source.Subscribe(list =>
        {
            if (list is null) return;
            for (int i = 0; i < list.Count; i++) observer.OnNext(list[i]);
        },
        observer.OnErrorResume,
        observer.OnCompleted);
    });
}

/// <summary>
/// Creates an observable sequence that depends on a resource object, disposing it when the sequence terminates.
/// </summary>
public static Observable<TResult> Using<TResource, TResult>(Func<TResource> resourceFactory, Func<TResource, Observable<TResult>> observableFactory)
    where TResource : IDisposable
{
    if (resourceFactory is null) throw new ArgumentNullException(nameof(resourceFactory));
    if (observableFactory is null) throw new ArgumentNullException(nameof(observableFactory));
    return Observable.Create<TResult>(observer =>
    {
        var resource = resourceFactory();
        var subscription = observableFactory(resource).Subscribe(observer);
        return Disposable.Combine(subscription, resource);
    });
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
/// Repeats the source sequence while the condition evaluates to true.
/// </summary>
public static Observable<T> While<T>(this Observable<T> source, Func<bool> condition)
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    if (condition is null) throw new ArgumentNullException(nameof(condition));
    return Observable.Defer(() =>
    {
        if (condition())
        {
            // Concat source then recurse via Defer
            return Observable.Concat(source, Observable.Defer(() => source.While(condition)));
        }
        else
        {
            return Observable.Empty<T>();
        }
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
        Lock gate = new();
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
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    observer.OnNext(new HeartbeatEvent<T>(true, default));
                }
            }, null, quietPeriod, quietPeriod);
        }

        upstream = source.Subscribe(
            x =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    observer.OnNext(new HeartbeatEvent<T>(false, x));
                    ScheduleHeartbeat();
                }
            },
            ex =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    observer.OnErrorResume(ex);
                }
            },
            r =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    DisposeTimer();
                    observer.OnCompleted(r);
                }
            });

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
        Lock gate = new();
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
                    using (gate.EnterScope())
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
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    observer.OnNext(new StaleEvent<T>(false, x));
                    ResetQuietTimer();
                }
            },
            ex =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    timer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                    observer.OnErrorResume(ex);
                }
            },
            r =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    timer?.Dispose();
                    observer.OnCompleted(r);
                }
            });

        return Disposable.Create(() =>
        {
            using (gate.EnterScope())
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
        Lock gate = new();
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
                    using (gate.EnterScope())
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
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    buffer.Add(x);
                    ResetQuietTimer();
                }
            },
            ex =>
            {
                using (gate.EnterScope())
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
                using (gate.EnterScope())
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
            using (gate.EnterScope())
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
        Lock gate = new();
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
                    using (gate.EnterScope())
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
                using (gate.EnterScope())
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
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    timer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
                    observer.OnErrorResume(ex);
                }
            },
            r =>
            {
                using (gate.EnterScope())
                {
                    if (disposed) return;
                    timer?.Dispose();
                    observer.OnCompleted(r);
                }
            });

        return Disposable.Create(() =>
        {
            using (gate.EnterScope())
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
/// Starts an action asynchronously and emits Unit on completion.
/// </summary>
public static Observable<Unit> Start(Action action, bool configureAwait = true)
{
    if (action is null) throw new ArgumentNullException(nameof(action));
    return Observable.FromAsync(
        _ =>
        {
            action();
            return default(ValueTask);
        },
        configureAwait);
}

/// <summary>
/// Starts a function asynchronously and emits its result, then completes.
/// </summary>
public static Observable<TResult> Start<TResult>(Func<TResult> func, bool configureAwait = true)
{
    if (func is null) throw new ArgumentNullException(nameof(func));
    return Observable.FromAsync(ct => new ValueTask<TResult>(func()), configureAwait);
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
/// Push multiple values to an observer.
/// </summary>
public static void OnNext<T>(this Observer<T> observer, params T[] values)
{
    if (observer is null) throw new ArgumentNullException(nameof(observer));
    if (values is null) return;
    for (int i = 0; i < values.Length; i++)
    {
        observer.OnNext(values[i]);
    }
}

/// <summary>
/// Push a sequence of values to an observer.
/// </summary>
public static void OnNext<T>(this Observer<T> observer, System.Collections.Generic.IEnumerable<T> values)
{
    if (observer is null) throw new ArgumentNullException(nameof(observer));
    if (values is null) return;
    foreach (var v in values)
    {
        observer.OnNext(v);
    }
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
/// Filter string values by a regular expression pattern.
/// </summary>
public static Observable<string> Filter(this Observable<string> source, string pattern, System.Text.RegularExpressions.RegexOptions options = System.Text.RegularExpressions.RegexOptions.None)
{
    if (source is null) throw new ArgumentNullException(nameof(source));
    if (pattern is null) throw new ArgumentNullException(nameof(pattern));
    var regex = new System.Text.RegularExpressions.Regex(pattern, options);
    return source.Where(s => s != null && regex.IsMatch(s));
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

/// <summary>
/// In-place Fisher-Yates shuffle for lists.
/// </summary>
public static void Shuffle<T>(this System.Collections.Generic.IList<T> list, Random? rng = null)
{
    if (list is null) throw new ArgumentNullException(nameof(list));
    rng ??= Random.Shared;
    for (int i = list.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (list[i], list[j]) = (list[j], list[i]);
    }
}

/// <summary>
/// In-place Fisher-Yates shuffle for arrays.
/// </summary>
public static void Shuffle<T>(this T[] array, Random? rng = null)
{
    if (array is null) throw new ArgumentNullException(nameof(array));
    ((System.Collections.Generic.IList<T>)array).Shuffle(rng);
}
}

/// <summary>
/// ReactiveUI-compatible command implementation for R3.
/// Drop-in replacement for ReactiveUI.ReactiveCommand with full API compatibility.
/// </summary>
/// <typeparam name="TInput">The type of parameter the command accepts</typeparam>
/// <typeparam name="TOutput">The type of value the command produces</typeparam>
public class ReactiveUICompatibleCommand<TInput, TOutput> : ICommand, IObservable<TOutput>, IDisposable
{
    private readonly Subject<TOutput> _executionResults = new();
    private readonly Subject<Exception> _thrownExceptions = new();
    private readonly ReactiveProperty<bool> _isExecuting = new(false);
    private readonly Func<TInput, CancellationToken, ValueTask<TOutput>> _executeAsync;
    private readonly Observable<bool> _canExecute;
    private readonly TimeProvider? _outputScheduler;
    private readonly DisposableBag _disposables = new();
    private bool _isDisposed;

    protected ReactiveUICompatibleCommand(
        Func<TInput, CancellationToken, ValueTask<TOutput>> executeAsync,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute ?? Observable.Return(true);
        _outputScheduler = outputScheduler;

        // Subscribe to CanExecute changes and trigger CanExecuteChanged
        _canExecute
            .DistinctUntilChanged()
            .Subscribe(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty))
            .AddTo(ref _disposables);
    }

    /// <summary>
    /// Observable stream of command execution results
    /// </summary>
    public Observable<TOutput> AsObservable() => _executionResults.AsObservable();

    /// <summary>
    /// Observable indicating whether the command is currently executing
    /// </summary>
    public Observable<bool> IsExecuting => _isExecuting;

    /// <summary>
    /// Observable indicating whether the command can currently execute
    /// </summary>
    public Observable<bool> CanExecute => _canExecute;

    /// <summary>
    /// Observable stream of exceptions thrown during command execution
    /// </summary>
    public Observable<Exception> ThrownExceptions => _thrownExceptions;

    /// <summary>
    /// Event fired when CanExecute status changes
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Execute the command with the given parameter, returning an observable of the result
    /// </summary>
    public Observable<TOutput> Execute(TInput parameter)
    {
        return Observable.Create<TOutput>(async (observer, ct) =>
        {
            if (_isDisposed)
            {
                observer.OnCompleted();
                return;
            }

            try
            {
                _isExecuting.Value = true;
                var result = await _executeAsync(parameter, ct);

                // Marshal to output scheduler if specified
                if (_outputScheduler != null)
                {
                    await Task.Delay(TimeSpan.Zero, _outputScheduler, ct); // Context switch to scheduler
                }

                _executionResults.OnNext(result);
                observer.OnNext(result);
                observer.OnCompleted();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _thrownExceptions.OnNext(ex);
                observer.OnErrorResume(ex);
                observer.OnCompleted(Result.Failure(ex));
            }
            finally
            {
                _isExecuting.Value = false;
            }
        });
    }

    /// <summary>
    /// Execute the command (parameterless variant)
    /// </summary>
    public Observable<TOutput> Execute()
    {
        return Execute(default!);
    }

    #region ICommand Implementation

    bool ICommand.CanExecute(object? parameter)
    {
        if (_isDisposed) return false;

        // For ReactiveProperty, we can get CurrentValue directly
        if (_canExecute is ReactiveProperty<bool> rp)
        {
            return rp.CurrentValue && !_isExecuting.CurrentValue;
        }

        // For other observables (including subjects), subscribe briefly to get latest value
        var canExecuteValue = true;
        var gotValue = false;
        using (var subscription = _canExecute.Subscribe(val =>
        {
            canExecuteValue = val;
            gotValue = true;
        }))
        {
            // Subscription completes immediately for subjects/behavior subjects
        }

        // If we didn't get a value (cold observable), default to true
        if (!gotValue)
            canExecuteValue = true;

        return canExecuteValue && !_isExecuting.CurrentValue;
    }

    void ICommand.Execute(object? parameter)
    {
        if (_isDisposed) return;

        TInput? typedParameter = parameter is TInput input ? input : default;
        Execute(typedParameter!).Subscribe(_ => { });
    }

    #endregion

    #region IObservable Implementation

    IDisposable IObservable<TOutput>.Subscribe(IObserver<TOutput> observer)
    {
        if (_isDisposed)
        {
            observer.OnCompleted();
            return Disposable.Create(() => { });
        }

        // Bridge R3.Subject to System.IObserver
        return _executionResults.Subscribe(
            onNext: value => observer.OnNext(value),
            onErrorResume: ex => observer.OnError(ex),
            onCompleted: result =>
            {
                if (result.IsSuccess)
                    observer.OnCompleted();
                else if (result.Exception != null)
                    observer.OnError(result.Exception);
            });
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a ReactiveCommand from a synchronous execution function
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> Create(
        Func<TInput, TOutput> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            (param, ct) => new ValueTask<TOutput>(execute(param)),
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an asynchronous Task-based execution function
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> CreateFromTask(
        Func<TInput, CancellationToken, Task<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            async (param, ct) => await execute(param, ct),
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an asynchronous Task-based execution function (no cancellation token)
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> CreateFromTask(
        Func<TInput, Task<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            async (param, ct) => await execute(param),
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an observable-based execution function
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> CreateFromObservable(
        Func<TInput, IObservable<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            async (param, ct) =>
            {
                var sysObservable = execute(param);
                var r3Observable = sysObservable.ToObservable();
                return await r3Observable.FirstAsync(ct);
            },
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an R3 Observable-based execution function
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> CreateFromR3Observable(
        Func<TInput, Observable<TOutput>> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            async (param, ct) =>
            {
                var observable = execute(param);
                return await observable.FirstAsync(ct);
            },
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a combined command that executes multiple child commands concurrently.
    /// The combined command can only execute when all child commands can execute.
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput[]> CreateCombined(
        params ReactiveUICompatibleCommand<TInput, TOutput>[] childCommands)
    {
        if (childCommands == null || childCommands.Length == 0)
            throw new ArgumentException("At least one child command is required", nameof(childCommands));

        // Combine CanExecute from all child commands - can only execute when ALL can execute
        var combinedCanExecute = childCommands
            .Select(cmd => cmd.CanExecute)
            .CombineLatestValuesAreAllTrue();

        return new ReactiveUICompatibleCommand<TInput, TOutput[]>(
            async (param, ct) =>
            {
                // Execute all child commands concurrently
                var tasks = childCommands
                    .Select(cmd => cmd.Execute(param).FirstAsync(ct))
                    .ToArray();

                return await Task.WhenAll(tasks);
            },
            combinedCanExecute,
            null);
    }

    /// <summary>
    /// Creates a command that runs on a background thread (ThreadPool)
    /// </summary>
    public static ReactiveUICompatibleCommand<TInput, TOutput> CreateRunInBackground(
        Func<TInput, TOutput> execute,
        Observable<bool>? canExecute = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return new ReactiveUICompatibleCommand<TInput, TOutput>(
            async (param, ct) => await Task.Run(() => execute(param), ct),
            canExecute,
            TimeProvider.System);
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _disposables.Dispose();
        _executionResults.Dispose();
        _thrownExceptions.Dispose();
        _isExecuting.Dispose();
    }
}

/// <summary>
/// Factory methods for creating non-generic ReactiveCommands (Unit-based commands)
/// </summary>
public static class ReactiveUICompatibleCommand
{
    /// <summary>
    /// Creates a ReactiveCommand from a synchronous action
    /// </summary>
    public static ReactiveUICompatibleCommand<Unit, Unit> Create(
        Action execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return ReactiveUICompatibleCommand<Unit, Unit>.Create(
            _ =>
            {
                execute();
                return Unit.Default;
            },
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an asynchronous Task-based action
    /// </summary>
    public static ReactiveUICompatibleCommand<Unit, Unit> CreateFromTask(
        Func<CancellationToken, Task> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return ReactiveUICompatibleCommand<Unit, Unit>.CreateFromTask(
            async (_, ct) =>
            {
                await execute(ct);
                return Unit.Default;
            },
            canExecute,
            outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand from an asynchronous Task-based action (no cancellation token)
    /// </summary>
    public static ReactiveUICompatibleCommand<Unit, Unit> CreateFromTask(
        Func<Task> execute,
        Observable<bool>? canExecute = null,
        TimeProvider? outputScheduler = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));

        return ReactiveUICompatibleCommand<Unit, Unit>.CreateFromTask(
            async (_, ct) =>
            {
                await execute();
                return Unit.Default;
            },
            canExecute,
            outputScheduler);
    }
}

/// <summary>
/// Extension methods for invoking ReactiveCommands from observables
/// </summary>
public static class ReactiveCommandExtensions
{
    /// <summary>
    /// Executes the command whenever the source observable emits a value,
    /// passing that value as the command parameter
    /// </summary>
    public static IDisposable InvokeCommand<TInput, TOutput>(
        this IObservable<TInput> source,
        ReactiveUICompatibleCommand<TInput, TOutput> command)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));

        return source
            .ToObservable()
            .WithLatestFrom(command.CanExecute, (value, canExecute) => (value, canExecute))
            .Where(x => x.canExecute)
            .Subscribe(x => command.Execute(x.value).Subscribe(_ => { }));
    }

    /// <summary>
    /// Executes the parameterless (Unit-based) command whenever the source observable emits a value
    /// </summary>
    public static IDisposable InvokeCommand<T>(
        this IObservable<T> source,
        ReactiveUICompatibleCommand<Unit, Unit> command)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (command == null) throw new ArgumentNullException(nameof(command));

        return source
            .ToObservable()
            .WithLatestFrom(command.CanExecute, (value, canExecute) => (value, canExecute))
            .Where(x => x.canExecute)
            .Subscribe(_ => command.Execute().Subscribe(_ => { }));
    }
}
