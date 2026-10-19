using R3;

namespace R3Ext;

/// <summary>
/// Side-effect operators that observe events without transforming the stream.
/// Note: R3 already ships a general-purpose Do(onNext, onErrorResume, onCompleted) method;
/// these focused overloads provide cleaner call sites for single-event side-effects.
/// </summary>
public static class SideEffectExtensions
{
    /// <summary>
    /// Execute <paramref name="action"/> on each OnErrorResume event without consuming the error.
    /// The error continues to propagate downstream.
    /// </summary>
    public static Observable<T> DoOnError<T>(this Observable<T> source, Action<Exception> action)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
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
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        action(ex);
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
    /// Execute <paramref name="action"/> when the sequence terminates (success or failure).
    /// </summary>
    public static Observable<T> DoOnComplete<T>(this Observable<T> source, Action<Result> action)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
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
                observer.OnErrorResume,
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        action(r);
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
    /// Execute <paramref name="action"/> only when the sequence completes successfully.
    /// </summary>
    public static Observable<T> DoOnComplete<T>(this Observable<T> source, Action action)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
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
                observer.OnErrorResume,
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (r.IsSuccess)
                        {
                            action();
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
    /// Execute <paramref name="action"/> on any terminal event (success or failure completion).
    /// Does NOT fire on OnErrorResume events, which are non-terminal in R3.
    /// </summary>
    public static Observable<T> DoOnTerminate<T>(this Observable<T> source, Action action)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
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
                observer.OnErrorResume,
                r =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        action();
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
    /// Execute <paramref name="action"/> after the downstream has been notified of the terminal
    /// event, rather than before. The action fires after OnCompleted is forwarded.
    /// </summary>
    public static Observable<T> DoAfterTerminate<T>(this Observable<T> source, Action action)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
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
                        action();
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
