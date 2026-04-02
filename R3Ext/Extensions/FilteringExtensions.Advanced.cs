using R3;

namespace R3Ext;

public static partial class FilteringExtensions
{
    /// <summary>
    /// Suppresses all OnNext values; only passes through OnCompleted and OnErrorResume.
    /// </summary>
    public static Observable<T> IgnoreElements<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
            source.Subscribe(
                _ => { },
                observer.OnErrorResume,
                observer.OnCompleted));
    }

    /// <summary>
    /// Emits <c>false</c> when the first value arrives then completes; emits <c>true</c> if the
    /// source completes without emitting any value.
    /// </summary>
    public static Observable<bool> IsEmpty<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<bool>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            bool hadValue = false;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    bool shouldComplete = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (!hadValue)
                        {
                            hadValue = true;
                            disposed = true;
                            shouldComplete = true;
                        }
                    }

                    if (shouldComplete)
                    {
                        observer.OnNext(false);
                        observer.OnCompleted();
                        upstream?.Dispose();
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
                    bool emitTrue = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        emitTrue = !hadValue;
                    }

                    if (emitTrue)
                    {
                        observer.OnNext(true);
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Emits <c>false</c> as soon as predicate fails then completes; emits <c>true</c> when the
    /// source completes successfully if all values passed the predicate.
    /// </summary>
    public static Observable<bool> Every<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<bool>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    bool shouldFail = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (!predicate(x))
                        {
                            disposed = true;
                            shouldFail = true;
                        }
                    }

                    if (shouldFail)
                    {
                        observer.OnNext(false);
                        observer.OnCompleted();
                        upstream?.Dispose();
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
                    bool emitTrue = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        emitTrue = r.IsSuccess;
                    }

                    if (emitTrue)
                    {
                        observer.OnNext(true);
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Alias for <see cref="Every{T}"/>. Emits <c>false</c> as soon as predicate fails; emits
    /// <c>true</c> on successful completion if all values passed.
    /// </summary>
    public static Observable<bool> All<T>(this Observable<T> source, Func<T, bool> predicate)
        => source.Every(predicate);

    /// <summary>
    /// Emits the first element matching <paramref name="predicate"/> then completes.
    /// </summary>
    public static Observable<T> Find<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return source.Where(predicate).Take(1);
    }

    /// <summary>
    /// Emits the zero-based index of the first element matching <paramref name="predicate"/> then
    /// completes. Completes without emitting if no match is found.
    /// </summary>
    public static Observable<int> FindIndex<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Observable.Create<int>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int index = 0;
            IDisposable? upstream = null;

            upstream = source.Subscribe(
                x =>
                {
                    bool found = false;
                    int foundIndex = 0;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (predicate(x))
                        {
                            foundIndex = index;
                            found = true;
                            disposed = true;
                        }
                        else
                        {
                            index++;
                        }
                    }

                    if (found)
                    {
                        observer.OnNext(foundIndex);
                        observer.OnCompleted();
                        upstream?.Dispose();
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Emits <paramref name="defaultValue"/> and completes if the source completes without emitting
    /// any value; otherwise forwards all values as-is.
    /// </summary>
    public static Observable<T> DefaultIfEmpty<T>(this Observable<T> source, T defaultValue)
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
                    bool emitDefault = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        emitDefault = r.IsSuccess && !hasValue;
                    }

                    if (emitDefault)
                    {
                        observer.OnNext(defaultValue);
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// If the source completes successfully without emitting any value, completes downstream with a
    /// failure result using the provided exception factory (defaults to
    /// <see cref="InvalidOperationException"/>).
    /// </summary>
    public static Observable<T> ThrowIfEmpty<T>(this Observable<T> source, Func<Exception>? exceptionFactory = null)
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
                    bool throwEmpty = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        throwEmpty = r.IsSuccess && !hasValue;
                    }

                    if (throwEmpty)
                    {
                        Exception ex = exceptionFactory?.Invoke()
                            ?? new InvalidOperationException("Sequence contains no elements.");
                        observer.OnCompleted(Result.Failure(ex));
                    }
                    else
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
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Emits the most-recent source value whenever the duration observable (produced by
    /// <paramref name="durationSelector"/> for each source value) fires. Resets on each new source value.
    /// </summary>
    public static Observable<T> Audit<T>(this Observable<T> source, Func<T, Observable<Unit>> durationSelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (durationSelector is null)
        {
            throw new ArgumentNullException(nameof(durationSelector));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? latest = default;
            bool hasLatest = false;
            IDisposable? upstream = null;
            IDisposable? durationSub = null;

            upstream = source.Subscribe(
                x =>
                {
                    IDisposable? oldDurationSub = null;
                    Observable<Unit>? duration = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        latest = x;
                        hasLatest = true;
                        oldDurationSub = durationSub;
                        durationSub = null;
                        duration = durationSelector(x);
                    }

                    oldDurationSub?.Dispose();

                    IDisposable newSub = duration!.Subscribe(
                        _ =>
                        {
                            T? toEmit = default;
                            bool shouldEmit = false;
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                if (hasLatest)
                                {
                                    toEmit = latest;
                                    hasLatest = false;
                                    shouldEmit = true;
                                }
                            }

                            if (shouldEmit)
                            {
                                observer.OnNext(toEmit!);
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
                        _ => { });

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            newSub.Dispose();
                            return;
                        }

                        durationSub = newSub;
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
                    IDisposable? sub = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        sub = durationSub;
                        durationSub = null;
                    }

                    sub?.Dispose();
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
                    durationSub?.Dispose();
                    upstream?.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Emits the most-recent source value after each fixed <paramref name="duration"/> window.
    /// </summary>
    public static Observable<T> AuditTime<T>(this Observable<T> source, TimeSpan duration, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;
        return source.Audit(_ => Observable.Timer(duration, tp).Select(_ => Unit.Default));
    }

    /// <summary>
    /// Emits the most-recent source value whenever <paramref name="sampler"/> emits, then resets.
    /// </summary>
    public static Observable<T> Sample<T>(this Observable<T> source, Observable<Unit> sampler)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (sampler is null)
        {
            throw new ArgumentNullException(nameof(sampler));
        }

        return source.Sample<T, Unit>(sampler);
    }

    /// <summary>
    /// Emits the most-recent source value whenever <paramref name="sampler"/> emits, then resets.
    /// </summary>
    public static Observable<T> Sample<T, TSample>(this Observable<T> source, Observable<TSample> sampler)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (sampler is null)
        {
            throw new ArgumentNullException(nameof(sampler));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? latest = default;
            bool hasLatest = false;
            IDisposable? upstream = null;
            IDisposable? samplerSub = null;

            upstream = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        latest = x;
                        hasLatest = true;
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
                    IDisposable? sub = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        sub = samplerSub;
                        samplerSub = null;
                    }

                    sub?.Dispose();
                    observer.OnCompleted(r);
                });

            samplerSub = sampler.Subscribe(
                _ =>
                {
                    T? toEmit = default;
                    bool shouldEmit = false;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        if (hasLatest)
                        {
                            toEmit = latest;
                            hasLatest = false;
                            shouldEmit = true;
                        }
                    }

                    if (shouldEmit)
                    {
                        observer.OnNext(toEmit!);
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
                    IDisposable? sub = null;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        disposed = true;
                        sub = upstream;
                        upstream = null;
                    }

                    sub?.Dispose();
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
                    upstream?.Dispose();
                    samplerSub?.Dispose();
                }
            });
        });
    }
}
