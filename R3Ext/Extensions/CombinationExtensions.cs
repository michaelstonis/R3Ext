using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace R3Ext;

/// <summary>
/// Combination and creation operator extensions for R3 observables.
/// </summary>
public static class CombinationExtensions
{
    /// <summary>
    /// Subscribes to all sources concurrently and emits an array of the last value from each when all complete.
    /// </summary>
    public static Observable<T[]> ForkJoin<T>(params Observable<T>[] sources)
        => ForkJoin((IEnumerable<Observable<T>>)sources);

    /// <summary>
    /// Subscribes to all sources concurrently and emits an array of the last value from each when all complete.
    /// </summary>
    public static Observable<T[]> ForkJoin<T>(IEnumerable<Observable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        Observable<T>[] sourceArray = sources.ToArray();

        if (sourceArray.Length == 0)
        {
            return Observable.Return(Array.Empty<T>());
        }

        return Observable.Create<T[]>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T?[] lastValues = new T?[sourceArray.Length];
            bool[] completed = new bool[sourceArray.Length];
            bool[] hasValue = new bool[sourceArray.Length];
            IDisposable?[] subscriptions = new IDisposable?[sourceArray.Length];

            void CheckCompletion()
            {
                for (int k = 0; k < completed.Length; k++)
                {
                    if (!completed[k])
                    {
                        return;
                    }
                }

                disposed = true;

                for (int k = 0; k < sourceArray.Length; k++)
                {
                    if (!hasValue[k])
                    {
                        observer.OnCompleted(Result.Failure(new InvalidOperationException($"Source at index {k} completed without emitting any value.")));
                        return;
                    }
                }

                T[] result = new T[sourceArray.Length];
                for (int k = 0; k < sourceArray.Length; k++)
                {
                    result[k] = lastValues[k]!;
                }

                observer.OnNext(result);
                observer.OnCompleted();
            }

            for (int i = 0; i < sourceArray.Length; i++)
            {
                int index = i;
                subscriptions[i] = sourceArray[i].Subscribe(
                    x =>
                    {
                        using (gate.EnterScope())
                        {
                            if (disposed)
                            {
                                return;
                            }

                            lastValues[index] = x;
                            hasValue[index] = true;
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

                            disposed = true;
                            observer.OnCompleted(Result.Failure(ex));
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

                            if (r.IsFailure)
                            {
                                disposed = true;
                                observer.OnCompleted(r);
                                return;
                            }

                            completed[index] = true;
                            CheckCompletion();
                        }
                    });
            }

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    for (int k = 0; k < subscriptions.Length; k++)
                    {
                        subscriptions[k]?.Dispose();
                    }
                }
            });
        });
    }

    /// <summary>
    /// Subscribes to both sources concurrently and emits a tuple of the last values when both complete.
    /// </summary>
    public static Observable<(T1 Item1, T2 Item2)> ForkJoin<T1, T2>(Observable<T1> source1, Observable<T2> source2)
    {
        ArgumentNullException.ThrowIfNull(source1);
        ArgumentNullException.ThrowIfNull(source2);

        return Observable.Create<(T1 Item1, T2 Item2)>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T1? last1 = default;
            T2? last2 = default;
            bool has1 = false;
            bool has2 = false;
            bool completed1 = false;
            bool completed2 = false;
            IDisposable? sub1 = null;
            IDisposable? sub2 = null;

            void CheckCompletion()
            {
                if (!completed1 || !completed2)
                {
                    return;
                }

                disposed = true;

                if (!has1 || !has2)
                {
                    observer.OnCompleted(Result.Failure(new InvalidOperationException("A source completed without emitting any value.")));
                    return;
                }

                observer.OnNext((last1!, last2!));
                observer.OnCompleted();
            }

            sub1 = source1.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        last1 = x;
                        has1 = true;
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

                        disposed = true;
                        observer.OnCompleted(Result.Failure(ex));
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            return;
                        }

                        completed1 = true;
                        CheckCompletion();
                    }
                });

            sub2 = source2.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        last2 = x;
                        has2 = true;
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

                        disposed = true;
                        observer.OnCompleted(Result.Failure(ex));
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            return;
                        }

                        completed2 = true;
                        CheckCompletion();
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
                    sub1?.Dispose();
                    sub2?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Subscribes to all three sources concurrently and emits a tuple of the last values when all complete.
    /// </summary>
    public static Observable<(T1 Item1, T2 Item2, T3 Item3)> ForkJoin<T1, T2, T3>(
        Observable<T1> source1, Observable<T2> source2, Observable<T3> source3)
    {
        ArgumentNullException.ThrowIfNull(source1);
        ArgumentNullException.ThrowIfNull(source2);
        ArgumentNullException.ThrowIfNull(source3);

        return Observable.Create<(T1 Item1, T2 Item2, T3 Item3)>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T1? last1 = default;
            T2? last2 = default;
            T3? last3 = default;
            bool has1 = false;
            bool has2 = false;
            bool has3 = false;
            bool completed1 = false;
            bool completed2 = false;
            bool completed3 = false;
            IDisposable? sub1 = null;
            IDisposable? sub2 = null;
            IDisposable? sub3 = null;

            void CheckCompletion()
            {
                if (!completed1 || !completed2 || !completed3)
                {
                    return;
                }

                disposed = true;

                if (!has1 || !has2 || !has3)
                {
                    observer.OnCompleted(Result.Failure(new InvalidOperationException("A source completed without emitting any value.")));
                    return;
                }

                observer.OnNext((last1!, last2!, last3!));
                observer.OnCompleted();
            }

            sub1 = source1.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        last1 = x;
                        has1 = true;
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

                        disposed = true;
                        observer.OnCompleted(Result.Failure(ex));
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            return;
                        }

                        completed1 = true;
                        CheckCompletion();
                    }
                });

            sub2 = source2.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        last2 = x;
                        has2 = true;
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

                        disposed = true;
                        observer.OnCompleted(Result.Failure(ex));
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            return;
                        }

                        completed2 = true;
                        CheckCompletion();
                    }
                });

            sub3 = source3.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        last3 = x;
                        has3 = true;
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

                        disposed = true;
                        observer.OnCompleted(Result.Failure(ex));
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            return;
                        }

                        completed3 = true;
                        CheckCompletion();
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
                    sub1?.Dispose();
                    sub2?.Dispose();
                    sub3?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Subscribes to sources sequentially, moving to the next source when the current one completes
    /// regardless of success or failure.
    /// </summary>
    public static Observable<T> OnErrorResumeNext<T>(params Observable<T>[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Length == 0)
        {
            return Observable.Empty<T>();
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? currentSub = null;

            void SubscribeAt(int i)
            {
                if (i >= sources.Length)
                {
                    observer.OnCompleted();
                    return;
                }

                bool hasAdvanced = false;

                void Advance()
                {
                    bool shouldAdvance;
                    using (gate.EnterScope())
                    {
                        if (disposed || hasAdvanced)
                        {
                            shouldAdvance = false;
                        }
                        else
                        {
                            hasAdvanced = true;
                            shouldAdvance = true;
                        }
                    }

                    if (shouldAdvance)
                    {
                        SubscribeAt(i + 1);
                    }
                }

                currentSub = sources[i].Subscribe(
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

                        Advance();
                    },
                    r =>
                    {
                        Advance();
                    });
            }

            SubscribeAt(0);

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    currentSub?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Continues with the next source when the current source completes (regardless of success or failure).
    /// </summary>
    public static Observable<T> OnErrorResumeNext<T>(this Observable<T> source, Observable<T> next)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(next);

        return OnErrorResumeNext(new Observable<T>[] { source, next });
    }

    /// <summary>
    /// At subscribe time, evaluates the condition and subscribes to the appropriate source.
    /// </summary>
    public static Observable<T> Iif<T>(
        Func<bool> condition, Observable<T> thenSource, Observable<T> elseSource)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenSource);
        ArgumentNullException.ThrowIfNull(elseSource);

        return Observable.Defer(() => condition() ? thenSource : elseSource);
    }

    /// <summary>
    /// At subscribe time, evaluates the condition and subscribes to the appropriate source.
    /// Alias for <see cref="Iif{T}"/>.
    /// </summary>
    public static Observable<T> Condition<T>(
        Func<bool> condition, Observable<T> thenSource, Observable<T> elseSource)
        => Iif(condition, thenSource, elseSource);

    /// <summary>
    /// Determines whether two observable sequences are equal by comparing elements pairwise.
    /// Emits <c>true</c> if both sequences have the same length and equal elements; <c>false</c> on first mismatch.
    /// </summary>
    public static Observable<bool> SequenceEqual<T>(
        this Observable<T> source,
        Observable<T> second,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(second);

        comparer ??= EqualityComparer<T>.Default;

        return Observable.Create<bool>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            Queue<T> q1 = new();
            Queue<T> q2 = new();
            bool s1Completed = false;
            bool s2Completed = false;
            IDisposable? sub1 = null;
            IDisposable? sub2 = null;

            void TryAdvance()
            {
                while (q1.Count > 0 && q2.Count > 0)
                {
                    T v1 = q1.Dequeue();
                    T v2 = q2.Dequeue();

                    if (!comparer.Equals(v1, v2))
                    {
                        disposed = true;
                        observer.OnNext(false);
                        observer.OnCompleted();
                        sub1?.Dispose();
                        sub2?.Dispose();
                        return;
                    }
                }

                if (s1Completed && s2Completed && q1.Count == 0 && q2.Count == 0)
                {
                    disposed = true;
                    observer.OnNext(true);
                    observer.OnCompleted();
                    return;
                }

                if (s1Completed && q1.Count == 0 && q2.Count > 0)
                {
                    disposed = true;
                    observer.OnNext(false);
                    observer.OnCompleted();
                    sub2?.Dispose();
                    return;
                }

                if (s2Completed && q2.Count == 0 && q1.Count > 0)
                {
                    disposed = true;
                    observer.OnNext(false);
                    observer.OnCompleted();
                    sub1?.Dispose();
                }
            }

            sub1 = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        q1.Enqueue(x);
                        TryAdvance();
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            sub2?.Dispose();
                            return;
                        }

                        s1Completed = true;
                        TryAdvance();
                    }
                });

            sub2 = second.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        q2.Enqueue(x);
                        TryAdvance();
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

                        if (r.IsFailure)
                        {
                            disposed = true;
                            observer.OnCompleted(r);
                            sub1?.Dispose();
                            return;
                        }

                        s2Completed = true;
                        TryAdvance();
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
                    sub1?.Dispose();
                    sub2?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Repeats the source observable when the handler observable emits; stops when the handler completes.
    /// </summary>
    public static Observable<T> RepeatWhen<T>(
        this Observable<T> source,
        Func<Observable<Exception?>, Observable<Unit>> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            Subject<Exception?> notifier = new();
            IDisposable? sourceSubscription = null;
            IDisposable? handlerSubscription = null;

            void SubscribeToSource()
            {
                sourceSubscription?.Dispose();
                sourceSubscription = source.Subscribe(
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
                        bool shouldNotify;
                        Exception? exc;
                        using (gate.EnterScope())
                        {
                            shouldNotify = !disposed;
                            exc = r.IsFailure ? r.Exception : null;
                        }

                        if (shouldNotify)
                        {
                            notifier.OnNext(exc);
                        }
                    });
            }

            handlerSubscription = handler(notifier).Subscribe(
                _ =>
                {
                    bool shouldResubscribe;
                    using (gate.EnterScope())
                    {
                        shouldResubscribe = !disposed;
                    }

                    if (shouldResubscribe)
                    {
                        SubscribeToSource();
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

                        disposed = true;
                        observer.OnCompleted(r);
                    }
                });

            SubscribeToSource();

            return Disposable.Create(() =>
            {
                using (gate.EnterScope())
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    sourceSubscription?.Dispose();
                    handlerSubscription?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Creates an observable sequence by iterating a state machine, applying a result selector to each state.
    /// </summary>
    public static Observable<TResult> Generate<TState, TResult>(
        TState initialState,
        Func<TState, bool> condition,
        Func<TState, TState> iterate,
        Func<TState, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(iterate);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return Observable.Create<TResult>(observer =>
        {
            TState state = initialState;
            while (condition(state))
            {
                observer.OnNext(resultSelector(state));
                state = iterate(state);
            }

            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    /// <summary>
    /// Creates an observable sequence by iterating a state machine, emitting each state value.
    /// </summary>
    public static Observable<TState> Generate<TState>(
        TState initialState,
        Func<TState, bool> condition,
        Func<TState, TState> iterate)
        => Generate(initialState, condition, iterate, static x => x);
}
