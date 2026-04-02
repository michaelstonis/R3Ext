using R3;

namespace R3Ext;

/// <summary>
/// Higher-order flat-mapping extensions for R3 observables.
/// </summary>
public static class FlatMapExtensions
{
    /// <summary>
    /// Projects each source value to an inner observable and concatenates them sequentially,
    /// waiting for each inner observable to complete before subscribing to the next.
    /// </summary>
    public static Observable<TResult> ConcatMap<T, TResult>(
        this Observable<T> source,
        Func<T, Observable<TResult>> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<TResult>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            bool innerActive = false;
            Queue<Observable<TResult>> pending = new();
            IDisposable? upstream = null;
            IDisposable? innerSub = null;
            Result sourceResult = default;

            void SubscribeToInner(Observable<TResult> inner)
            {
                IDisposable sub = inner.Subscribe(
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
                        Observable<TResult>? nextInner = null;
                        bool shouldComplete = false;
                        Result completionResult = r;

                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            if (pending.Count > 0)
                            {
                                nextInner = pending.Dequeue();
                            }
                            else
                            {
                                innerActive = false;
                                if (sourceCompleted)
                                {
                                    shouldComplete = true;
                                    completionResult = sourceResult;
                                }
                            }
                        }

                        if (nextInner is not null)
                        {
                            SubscribeToInner(nextInner);
                        }
                        else if (shouldComplete)
                        {
                            observer.OnCompleted(completionResult);
                        }
                    });

                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        sub.Dispose();
                        return;
                    }

                    innerSub = sub;
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    Observable<TResult>? toSubscribe = null;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        Observable<TResult> inner = selector(x);

                        if (!innerActive)
                        {
                            innerActive = true;
                            toSubscribe = inner;
                        }
                        else
                        {
                            pending.Enqueue(inner);
                        }
                    }

                    if (toSubscribe is not null)
                    {
                        SubscribeToInner(toSubscribe);
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        sourceResult = r;
                        shouldComplete = !innerActive && pending.Count == 0;
                    }

                    if (shouldComplete)
                    {
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
                    innerSub?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Projects each source value to an inner observable, cancelling any previously active inner
    /// observable when a new source value arrives. Only values from the most recent inner observable
    /// are forwarded downstream.
    /// </summary>
    public static Observable<TResult> SwitchMap<T, TResult>(
        this Observable<T> source,
        Func<T, Observable<TResult>> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<TResult>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            bool innerActive = false;
            int innerGeneration = 0;
            IDisposable? upstream = null;
            IDisposable? innerSub = null;

            upstream = source.Subscribe(
                x =>
                {
                    IDisposable? oldSub;
                    Observable<TResult> inner;
                    int myGeneration;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        oldSub = innerSub;
                        innerSub = null;
                        innerActive = true;
                        myGeneration = ++innerGeneration;
                        inner = selector(x);
                    }

                    oldSub?.Dispose();

                    IDisposable sub = inner.Subscribe(
                        v =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                observer.OnNext(v);
                            }
                        },
                        ex =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                observer.OnErrorResume(ex);
                            }
                        },
                        r =>
                        {
                            bool shouldComplete;

                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                innerActive = false;
                                shouldComplete = sourceCompleted;
                            }

                            if (shouldComplete)
                            {
                                observer.OnCompleted(r);
                            }
                        });

                    using (gate.EnterScope())
                    {
                        if (disposed || innerGeneration != myGeneration)
                        {
                            sub.Dispose();
                            return;
                        }

                        innerSub = sub;
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        shouldComplete = !innerActive;
                    }

                    if (shouldComplete)
                    {
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
                    innerSub?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Alias for <see cref="SwitchMap{T,TResult}"/>. Projects to the latest inner observable,
    /// dropping earlier ones when a new source value arrives.
    /// </summary>
    public static Observable<TResult> FlatMapLatest<T, TResult>(
        this Observable<T> source,
        Func<T, Observable<TResult>> selector)
        => source.SwitchMap(selector);

    /// <summary>
    /// Projects each source value to an inner observable, but ignores new source values while an
    /// inner observable is still active. Only begins a new inner subscription once the current one
    /// completes.
    /// </summary>
    public static Observable<TResult> ExhaustMap<T, TResult>(
        this Observable<T> source,
        Func<T, Observable<TResult>> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<TResult>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            bool innerActive = false;
            Result sourceResult = default;
            IDisposable? upstream = null;
            IDisposable? innerSub = null;

            upstream = source.Subscribe(
                x =>
                {
                    Observable<TResult>? toSubscribe = null;

                    using (gate.EnterScope())
                    {
                        if (disposed || innerActive)
                        {
                            return;
                        }

                        innerActive = true;
                        toSubscribe = selector(x);
                    }

                    if (toSubscribe is null)
                    {
                        return;
                    }

                    IDisposable sub = toSubscribe.Subscribe(
                        v =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                observer.OnNext(v);
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
                            bool shouldComplete;

                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                innerActive = false;
                                shouldComplete = sourceCompleted;
                            }

                            if (shouldComplete)
                            {
                                observer.OnCompleted(sourceResult);
                            }
                        });

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            sub.Dispose();
                            return;
                        }

                        innerSub = sub;
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        sourceResult = r;
                        shouldComplete = !innerActive;
                    }

                    if (shouldComplete)
                    {
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
                    innerSub?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Recursively projects each emitted value through the selector to produce additional values,
    /// subscribing to all resulting inner observables concurrently (breadth-first expansion).
    /// </summary>
    public static Observable<T> Expand<T>(this Observable<T> source, Func<T, Observable<T>> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            int activeCount = 0;
            IDisposable? upstream = null;
            List<IDisposable> innerSubs = new();

            void SubscribeToInner(Observable<T> inner)
            {
                IDisposable sub = inner.Subscribe(
                    v =>
                    {
                        Observable<T> next;

                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            observer.OnNext(v);
                            activeCount++;
                            next = selector(v);
                        }

                        SubscribeToInner(next);
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
                        bool shouldComplete;

                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            activeCount--;
                            shouldComplete = activeCount == 0 && sourceCompleted;
                        }

                        if (shouldComplete)
                        {
                            observer.OnCompleted(r);
                        }
                    });

                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        sub.Dispose();
                        return;
                    }

                    innerSubs.Add(sub);
                }
            }

            upstream = source.Subscribe(
                x =>
                {
                    Observable<T> inner;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        observer.OnNext(x);
                        activeCount++;
                        inner = selector(x);
                    }

                    SubscribeToInner(inner);
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        shouldComplete = activeCount == 0;
                    }

                    if (shouldComplete)
                    {
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                List<IDisposable> subs;

                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    subs = innerSubs;
                    innerSubs = new List<IDisposable>();
                }

                foreach (IDisposable s in subs)
                {
                    s.Dispose();
                }

                upstream?.Dispose();
            });
        });
    }

    /// <summary>
    /// Like <c>Scan</c> but the accumulator returns an observable. All inner observables are
    /// subscribed to concurrently (merged), and each emission updates the running accumulator state.
    /// </summary>
    public static Observable<TAccumulate> MergeScan<TSource, TAccumulate>(
        this Observable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, Observable<TAccumulate>> accumulator)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (accumulator is null)
        {
            throw new ArgumentNullException(nameof(accumulator));
        }

        return Observable.Create<TAccumulate>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            int activeInners = 0;
            TAccumulate current = seed;
            IDisposable? upstream = null;
            List<IDisposable> innerSubs = new();

            upstream = source.Subscribe(
                x =>
                {
                    Observable<TAccumulate> inner;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        inner = accumulator(current, x);
                        activeInners++;
                    }

                    IDisposable sub = inner.Subscribe(
                        v =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                current = v;
                                observer.OnNext(v);
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
                            bool shouldComplete;

                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                activeInners--;
                                shouldComplete = sourceCompleted && activeInners == 0;
                            }

                            if (shouldComplete)
                            {
                                observer.OnCompleted(r);
                            }
                        });

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            sub.Dispose();
                            return;
                        }

                        innerSubs.Add(sub);
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        shouldComplete = activeInners == 0;
                    }

                    if (shouldComplete)
                    {
                        observer.OnCompleted(r);
                    }
                });

            return Disposable.Create(() =>
            {
                List<IDisposable> subs;

                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    subs = innerSubs;
                    innerSubs = new List<IDisposable>();
                }

                foreach (IDisposable s in subs)
                {
                    s.Dispose();
                }

                upstream?.Dispose();
            });
        });
    }

    /// <summary>
    /// Like <c>MergeScan</c> but switches to each new inner observable (cancelling the previous one)
    /// rather than merging all concurrently.
    /// </summary>
    public static Observable<TAccumulate> SwitchScan<TSource, TAccumulate>(
        this Observable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, Observable<TAccumulate>> accumulator)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (accumulator is null)
        {
            throw new ArgumentNullException(nameof(accumulator));
        }

        return Observable.Create<TAccumulate>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool sourceCompleted = false;
            bool innerActive = false;
            int innerGeneration = 0;
            TAccumulate current = seed;
            IDisposable? upstream = null;
            IDisposable? innerSub = null;

            upstream = source.Subscribe(
                x =>
                {
                    IDisposable? oldSub;
                    Observable<TAccumulate> inner;
                    int myGeneration;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        oldSub = innerSub;
                        innerSub = null;
                        innerActive = true;
                        myGeneration = ++innerGeneration;
                        inner = accumulator(current, x);
                    }

                    oldSub?.Dispose();

                    IDisposable sub = inner.Subscribe(
                        v =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                current = v;
                                observer.OnNext(v);
                            }
                        },
                        ex =>
                        {
                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                observer.OnErrorResume(ex);
                            }
                        },
                        r =>
                        {
                            bool shouldComplete;

                            using (gate.EnterScope())
                            {
                                if (disposed || innerGeneration != myGeneration)
                                {
                                    return;
                                }

                                innerActive = false;
                                shouldComplete = sourceCompleted;
                            }

                            if (shouldComplete)
                            {
                                observer.OnCompleted(r);
                            }
                        });

                    using (gate.EnterScope())
                    {
                        if (disposed || innerGeneration != myGeneration)
                        {
                            sub.Dispose();
                            return;
                        }

                        innerSub = sub;
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
                    bool shouldComplete;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        sourceCompleted = true;
                        shouldComplete = !innerActive;
                    }

                    if (shouldComplete)
                    {
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
                    innerSub?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }
}
