using R3;

namespace R3Ext;

public readonly struct TimeInterval<T>
{
    public T Value { get; }

    public TimeSpan Interval { get; }

    public TimeInterval(T value, TimeSpan interval)
    {
        Value = value;
        Interval = interval;
    }

    public void Deconstruct(out T value, out TimeSpan interval)
    {
        value = Value;
        interval = Interval;
    }
}

public enum OverflowStrategy
{
    DropOldest,
    DropLatest,
    Error,
}

public static partial class TimingExtensions
{
    /// <summary>
    /// Wraps each emitted value with the elapsed time since the previous emission.
    /// The first item is wrapped with <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public static Observable<TimeInterval<T>> TimeInterval<T>(
        this Observable<T> source,
        TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<TimeInterval<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            DateTimeOffset? lastTime = null;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    TimeInterval<T> item;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        DateTimeOffset now = tp.GetUtcNow();
                        TimeSpan interval = lastTime.HasValue ? now - lastTime.Value : TimeSpan.Zero;
                        lastTime = now;
                        item = new TimeInterval<T>(x, interval);
                    }

                    observer.OnNext(item);
                },
                ex =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Delays each element by an amount determined by a per-element observable.
    /// The element is emitted when its duration observable emits its first value.
    /// Multiple elements can be in-flight simultaneously.
    /// </summary>
    public static Observable<T> DelayWhen<T>(
        this Observable<T> source,
        Func<T, Observable<Unit>> delayDurationSelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (delayDurationSelector is null)
        {
            throw new ArgumentNullException(nameof(delayDurationSelector));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            Result completionResult = default;
            int activeInner = 0;
            IDisposable? upstream = null;
            List<IDisposable> innerSubs = new();

            void CheckComplete()
            {
                // Must be called under gate.
                if (sourceCompleted && activeInner == 0)
                {
                    observer.OnCompleted(completionResult);
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    bool shouldContinue;
                    using (gate.EnterScope())
                    {
                        shouldContinue = !disposed;
                        if (shouldContinue)
                        {
                            activeInner++;
                        }
                    }

                    if (!shouldContinue)
                    {
                        return;
                    }

                    bool innerFired = false;
                    IDisposable? innerSub = null;

                    innerSub = delayDurationSelector(x).Subscribe(
                        _ =>
                        {
                            bool shouldEmit;
                            using (gate.EnterScope())
                            {
                                shouldEmit = !disposed && !innerFired;
                                if (!innerFired)
                                {
                                    innerFired = true;
                                    activeInner--;
                                    if (innerSub is not null)
                                    {
                                        innerSubs.Remove(innerSub);
                                    }
                                }
                            }

                            if (shouldEmit)
                            {
                                observer.OnNext(x);
                                using (gate.EnterScope())
                                {
                                    CheckComplete();
                                }
                            }
                        },
                        ex =>
                        {
                            bool wasActive;
                            using (gate.EnterScope())
                            {
                                wasActive = !innerFired;
                                if (!innerFired)
                                {
                                    innerFired = true;
                                    activeInner--;
                                    if (innerSub is not null)
                                    {
                                        innerSubs.Remove(innerSub);
                                    }
                                }
                            }

                            if (wasActive && !disposed)
                            {
                                observer.OnErrorResume(ex);
                            }
                        },
                        r =>
                        {
                            using (gate.EnterScope())
                            {
                                if (!innerFired)
                                {
                                    innerFired = true;
                                    activeInner--;
                                    if (innerSub is not null)
                                    {
                                        innerSubs.Remove(innerSub);
                                    }

                                    CheckComplete();
                                }
                            }
                        });

                    // Register the subscription for cleanup; handle the case where the
                    // inner observable fired synchronously (innerFired already true).
                    using (gate.EnterScope())
                    {
                        if (!disposed && !innerFired && innerSub is not null)
                        {
                            innerSubs.Add(innerSub);
                        }
                        else if (!innerFired && disposed)
                        {
                            innerFired = true;
                            activeInner--;
                            innerSub?.Dispose();
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
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        completionResult = r;
                        CheckComplete();
                    }
                });

            return Disposable.Create(() =>
            {
                List<IDisposable> toDispose;
                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    toDispose = new List<IDisposable>(innerSubs);
                    innerSubs.Clear();
                }

                upstream?.Dispose();
                foreach (IDisposable d in toDispose)
                {
                    d.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Delays each element by an amount determined by a per-element observable,
    /// and delays the subscription to <paramref name="source"/> until
    /// <paramref name="subscriptionDelay"/> emits its first value.
    /// </summary>
    public static Observable<T> DelayWhen<T>(
        this Observable<T> source,
        Func<T, Observable<Unit>> delayDurationSelector,
        Observable<Unit> subscriptionDelay)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (delayDurationSelector is null)
        {
            throw new ArgumentNullException(nameof(delayDurationSelector));
        }

        if (subscriptionDelay is null)
        {
            throw new ArgumentNullException(nameof(subscriptionDelay));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? delaySub = null;
            IDisposable? mainSub = null;

            delaySub = subscriptionDelay.Subscribe(
                _ =>
                {
                    bool shouldSubscribe;
                    using (gate.EnterScope())
                    {
                        shouldSubscribe = !disposed && mainSub is null;
                    }

                    if (!shouldSubscribe)
                    {
                        return;
                    }

                    IDisposable inner = source.DelayWhen(delayDurationSelector).Subscribe(
                        v => observer.OnNext(v),
                        ex => observer.OnErrorResume(ex),
                        r => observer.OnCompleted(r));

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            inner.Dispose();
                        }
                        else
                        {
                            mainSub = inner;
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
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed || mainSub is not null)
                        {
                            return;
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
                }

                delaySub?.Dispose();
                mainSub?.Dispose();
            });
        });
    }

    /// <summary>
    /// Allows at most <paramref name="count"/> items per <paramref name="period"/>.
    /// Excess items are queued and emitted in subsequent windows.
    /// </summary>
    public static Observable<T> RateLimit<T>(
        this Observable<T> source,
        int count,
        TimeSpan period,
        TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
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
            bool sourceCompleted = false;
            Result completionResult = default;
            int emittedThisWindow = 0;
            Queue<T> queue = new();
            IDisposable? upstream = null;
            ITimer? timer = null;

            void EnsureTimer()
            {
                if (timer is null)
                {
                    timer = tp.CreateTimer(
                        _ =>
                        {
                            List<T>? toEmit = null;
                            bool complete = false;
                            Result result = default;

                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                emittedThisWindow = 0;

                                if (queue.Count > 0)
                                {
                                    toEmit = new List<T>();
                                    int drainCount = Math.Min(queue.Count, count);
                                    for (int i = 0; i < drainCount; i++)
                                    {
                                        toEmit.Add(queue.Dequeue());
                                        emittedThisWindow++;
                                    }
                                }

                                if (sourceCompleted && queue.Count == 0)
                                {
                                    complete = true;
                                    result = completionResult;
                                    timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                }
                            }

                            if (toEmit is not null)
                            {
                                foreach (T item in toEmit)
                                {
                                    observer.OnNext(item);
                                }
                            }

                            if (complete)
                            {
                                observer.OnCompleted(result);
                            }
                        },
                        null, period, period);
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    bool shouldEmit;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        EnsureTimer();

                        if (emittedThisWindow < count)
                        {
                            emittedThisWindow++;
                            shouldEmit = true;
                        }
                        else
                        {
                            queue.Enqueue(x);
                            shouldEmit = false;
                        }
                    }

                    if (shouldEmit)
                    {
                        observer.OnNext(x);
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
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    List<T>? remaining = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        completionResult = r;
                        timer?.Dispose();

                        if (queue.Count > 0)
                        {
                            remaining = queue.ToList();
                            queue.Clear();
                        }
                    }

                    if (remaining is not null)
                    {
                        foreach (T item in remaining)
                        {
                            observer.OnNext(item);
                        }
                    }

                    observer.OnCompleted(r);
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
                    queue.Clear();
                }
            });
        });
    }

    /// <summary>
    /// Passes items through while maintaining an internal bounded buffer.
    /// When the buffer is at <paramref name="capacity"/>, the configured
    /// <paramref name="strategy"/> determines how overflow is handled.
    /// </summary>
    public static Observable<T> BufferWithOverflow<T>(
        this Observable<T> source,
        int capacity,
        OverflowStrategy strategy = OverflowStrategy.DropOldest,
        TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            Queue<T> buffer = new();
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    bool shouldEmit = true;
                    Exception? overflowError = null;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (buffer.Count >= capacity)
                        {
                            switch (strategy)
                            {
                                case OverflowStrategy.DropOldest:
                                    buffer.Dequeue();
                                    buffer.Enqueue(x);
                                    break;

                                case OverflowStrategy.DropLatest:
                                    shouldEmit = false;
                                    break;

                                case OverflowStrategy.Error:
                                    overflowError = new InvalidOperationException(
                                        $"Buffer overflow: capacity {capacity} exceeded.");
                                    shouldEmit = false;
                                    break;
                            }
                        }
                        else
                        {
                            buffer.Enqueue(x);
                        }
                    }

                    if (overflowError is not null)
                    {
                        observer.OnErrorResume(overflowError);
                    }
                    else if (shouldEmit)
                    {
                        observer.OnNext(x);
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
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
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
                    upstream?.Dispose();
                    buffer.Clear();
                }
            });
        });
    }

    /// <summary>
    /// Emits overlapping or non-overlapping arrays of a specified size, advancing by
    /// <paramref name="step"/> items between each window.
    /// When <paramref name="step"/> is 0, it defaults to <paramref name="size"/> (non-overlapping).
    /// Partial chunks at completion are discarded.
    /// </summary>
    public static Observable<T[]> Chunked<T>(
        this Observable<T> source,
        int size,
        int step = 0)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (step < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        int effectiveStep = step == 0 ? size : step;

        return Observable.Create<T[]>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            List<List<T>> openChunks = new();
            int itemCount = 0;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    List<T[]>? toEmit = null;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (itemCount % effectiveStep == 0)
                        {
                            openChunks.Add(new List<T>());
                        }

                        foreach (List<T> chunk in openChunks)
                        {
                            chunk.Add(x);
                        }

                        itemCount++;

                        // Collect completed chunks back-to-front to allow safe RemoveAt,
                        // then reverse so they are emitted oldest-first.
                        for (int i = openChunks.Count - 1; i >= 0; i--)
                        {
                            if (openChunks[i].Count >= size)
                            {
                                toEmit ??= new List<T[]>();
                                toEmit.Add(openChunks[i].ToArray());
                                openChunks.RemoveAt(i);
                            }
                        }

                        toEmit?.Reverse();
                    }

                    if (toEmit is not null)
                    {
                        foreach (T[] chunk in toEmit)
                        {
                            observer.OnNext(chunk);
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
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        openChunks.Clear();
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
                    upstream?.Dispose();
                    openChunks.Clear();
                }
            });
        });
    }
}
