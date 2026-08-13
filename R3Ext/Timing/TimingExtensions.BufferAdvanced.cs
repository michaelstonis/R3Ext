#pragma warning disable SA1107, SA1124, SA1501, SA1503, SA1515, SA1025
using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
    /// <summary>
    /// Collects source items into buffers that start when <paramref name="openings"/> emits and
    /// close when the corresponding observable returned by <paramref name="closingSelector"/> emits.
    /// Multiple buffers may be open simultaneously.
    /// </summary>
    public static Observable<T[]> BufferToggle<T, TOpen, TClose>(
        this Observable<T> source,
        Observable<TOpen> openings,
        Func<TOpen, Observable<TClose>> closingSelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (openings is null)
        {
            throw new ArgumentNullException(nameof(openings));
        }

        if (closingSelector is null)
        {
            throw new ArgumentNullException(nameof(closingSelector));
        }

        return Observable.Create<T[]>(observer =>
        {
            Lock gate = new();
            bool disposed = false;

            // ClosingSub may be null while we're between AddBuffer and the Subscribe return
            List<(List<T> Buffer, IDisposable? ClosingSub)> openBuffers = new();
            IDisposable? sourceSub = null;
            IDisposable? openingsSub = null;

            openingsSub = openings.Subscribe(
                opening =>
                {
                    List<T> newBuffer = new();

                    // Add buffer before subscribing to the closer so it is visible to the close
                    // callback even when the closer fires synchronously.
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        openBuffers.Add((newBuffer, null));
                    }

                    IDisposable closingSub = closingSelector(opening).Take(1).Subscribe(
                        _ =>
                        {
                            T[]? toEmit = null;
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                int idx = openBuffers.FindIndex(b => ReferenceEquals(b.Buffer, newBuffer));
                                if (idx >= 0)
                                {
                                    toEmit = openBuffers[idx].Buffer.ToArray();
                                    openBuffers.RemoveAt(idx);
                                }
                            }

                            if (toEmit is not null)
                            {
                                observer.OnNext(toEmit);
                            }
                        },
                        ex => observer.OnErrorResume(ex),
                        _ => { });

                    // Store the real closing subscription, or dispose immediately if already closed
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            closingSub.Dispose();
                            return;
                        }

                        int idx = openBuffers.FindIndex(b => ReferenceEquals(b.Buffer, newBuffer));
                        if (idx >= 0)
                        {
                            openBuffers[idx] = (openBuffers[idx].Buffer, closingSub);
                        }
                        else
                        {
                            // Closed synchronously before we could store the sub; dispose it now
                            closingSub.Dispose();
                        }
                    }
                },
                ex =>
                {
                    // OnErrorResume is non-terminal: forward it but keep open buffers and their
                    // closing subscriptions alive so windowing continues after a resumable error.
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }
                    }

                    observer.OnErrorResume(ex);
                },
                _ => { }); // openings completing does not close the outer sequence

            sourceSub = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        foreach (var (buf, _) in openBuffers)
                        {
                            buf.Add(x);
                        }
                    }
                },
                ex =>
                {
                    // OnErrorResume is non-terminal: forward it but keep open buffers (and the items
                    // already collected in them) alive so values after a resumable error are not dropped.
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
                    T[][]? toEmit;
                    IDisposable[]? subs;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        toEmit = openBuffers.Select(b => b.Buffer.ToArray()).ToArray();
                        subs = openBuffers.Select(b => b.ClosingSub).Where(s => s is not null).ToArray()!;
                        openBuffers.Clear();
                    }

                    foreach (IDisposable s in subs)
                    {
                        s.Dispose();
                    }

                    foreach (T[] arr in toEmit)
                    {
                        observer.OnNext(arr);
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
                    openingsSub?.Dispose();
                    sourceSub?.Dispose();

                    foreach (var (_, sub) in openBuffers)
                    {
                        sub?.Dispose();
                    }

                    openBuffers.Clear();
                }
            });
        });
    }

    /// <summary>
    /// Collects source items into a single buffer. When the observable returned by
    /// <paramref name="closingSelector"/> emits, the current buffer is emitted and a fresh
    /// buffer is started with a new invocation of <paramref name="closingSelector"/>.
    /// </summary>
    public static Observable<T[]> BufferWhen<T, TClose>(
        this Observable<T> source,
        Func<Observable<TClose>> closingSelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (closingSelector is null)
        {
            throw new ArgumentNullException(nameof(closingSelector));
        }

        return Observable.Create<T[]>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            List<T> buffer = new();
            IDisposable? sourceSub = null;
            IDisposable? closingSub = null;

            void SubscribeToCloser()
            {
                // Re-subscribe iteratively. A closing observable that emits synchronously on
                // subscription (e.g. Observable.Return) would otherwise recurse through this
                // method once per close and overflow the stack; the loop keeps that flat.
                bool resubscribe = true;
                while (resubscribe)
                {
                    resubscribe = false;
                    bool subscribed = false;
                    IDisposable? sub = null;

                    sub = closingSelector().Take(1).Subscribe(
                        _ =>
                        {
                            T[] toEmit;
                            using (gate.EnterScope())
                            {
                                if (disposed)
                                {
                                    return;
                                }

                                toEmit = buffer.ToArray();
                                buffer.Clear();
                            }

                            observer.OnNext(toEmit);

                            if (subscribed)
                            {
                                // Asynchronous close (after Subscribe returned): a fresh call
                                // stack, so re-subscribing recursively here is safe.
                                SubscribeToCloser();
                            }
                            else
                            {
                                // Synchronous close (during Subscribe): loop instead of recursing.
                                resubscribe = true;
                            }
                        },
                        ex => observer.OnErrorResume(ex),
                        _ => { });

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            sub?.Dispose();
                            return;
                        }

                        closingSub = sub;
                    }

                    subscribed = true;
                }
            }

            SubscribeToCloser();

            sourceSub = source.Subscribe(
                x =>
                {
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        buffer.Add(x);
                    }
                },
                ex =>
                {
                    T[] toEmit;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        closingSub?.Dispose();
                        closingSub = null;
                        toEmit = buffer.ToArray();
                        buffer.Clear();
                    }

                    observer.OnNext(toEmit);
                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    T[] toEmit;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        closingSub?.Dispose();
                        closingSub = null;
                        toEmit = buffer.ToArray();
                        buffer.Clear();
                    }

                    observer.OnNext(toEmit);
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
                    closingSub?.Dispose();
                    sourceSub?.Dispose();
                    buffer.Clear();
                }
            });
        });
    }
}
