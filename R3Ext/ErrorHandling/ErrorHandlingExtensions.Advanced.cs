using R3;

namespace R3Ext;

public static partial class ErrorHandlingExtensions
{
    /// <summary>
    /// Retry with a user-controlled notifier observable. When source fails the notifier receives
    /// the exception. If the notifier emits a value the source is re-subscribed. If the notifier
    /// completes the downstream completes.
    /// </summary>
    public static Observable<T> RetryWhen<T>(
        this Observable<T> source,
        Func<Observable<Exception?>, Observable<Unit>> handler)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? upstream = null;
            IDisposable? retrySubscription = null;
            Subject<Exception?> notifier = new();
            Observable<Unit> retrySignal = handler(notifier);

            void SubscribeOnce()
            {
                upstream?.Dispose();
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
                        // Notify outside the gate to avoid reentrancy deadlock: the handler
                        // may synchronously re-trigger SubscribeOnce via the retrySignal.
                        bool isDisposed;
                        using (gate.EnterScope())
                        {
                            isDisposed = disposed;
                        }

                        if (isDisposed)
                        {
                            return;
                        }

                        if (r.IsFailure)
                        {
                            notifier.OnNext(r.Exception);
                        }
                        else
                        {
                            notifier.OnCompleted();
                        }
                    });
            }

            retrySubscription = retrySignal.Subscribe(
                _ =>
                {
                    bool isDisposed;
                    using (gate.EnterScope())
                    {
                        isDisposed = disposed;
                    }

                    if (!isDisposed)
                    {
                        SubscribeOnce();
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

                        observer.OnCompleted(r);
                    }
                });

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
                    upstream?.Dispose();
                    retrySubscription?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// On any OnErrorResume event, emit <paramref name="fallbackValue"/> and complete the sequence.
    /// Unlike <see cref="CatchAndReturn{T}"/> (which handles terminal failure completions), this
    /// intercepts non-terminal OnErrorResume events.
    /// </summary>
    public static Observable<T> ReplaceError<T>(this Observable<T> source, T fallbackValue)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
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
                    IDisposable? toDispose = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        toDispose = upstream;
                        observer.OnNext(fallbackValue);
                        observer.OnCompleted();
                    }

                    toDispose?.Dispose();
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
    /// If source completes with success but emitted no values, emit <paramref name="fallbackValue"/>
    /// before the completion. Equivalent to DefaultIfEmpty.
    /// </summary>
    public static Observable<T> ReplaceEmpty<T>(this Observable<T> source, T fallbackValue)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool hasValue = false;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        hasValue = true;
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

                        if (r.IsSuccess && !hasValue)
                        {
                            observer.OnNext(fallbackValue);
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
    /// Like Select but catches exceptions thrown by <paramref name="selector"/> and routes them
    /// to OnErrorResume instead of crashing the pipeline.
    /// </summary>
    public static Observable<TResult> SelectSafe<T, TResult>(
        this Observable<T> source,
        Func<T, TResult> selector)
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
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        try
                        {
                            observer.OnNext(selector(x));
                        }
                        catch (Exception ex)
                        {
                            observer.OnErrorResume(ex);
                        }
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
    /// Like Where but catches exceptions thrown by <paramref name="predicate"/> and routes them
    /// to OnErrorResume instead of crashing the pipeline.
    /// </summary>
    public static Observable<T> WhereSafe<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        try
                        {
                            if (predicate(x))
                            {
                                observer.OnNext(x);
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnErrorResume(ex);
                        }
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
}
