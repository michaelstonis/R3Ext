using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
    /// <summary>
    /// Projects each element of an observable sequence into zero or more windows, each containing
    /// <paramref name="count"/> elements. When <paramref name="skip"/> is less than
    /// <paramref name="count"/>, windows overlap; when equal (the default), they are contiguous.
    /// </summary>
    public static Observable<Observable<T>> WindowCount<T>(this Observable<T> source, int count, int skip = 0)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (skip == 0)
        {
            skip = count;
        }

        return Observable.Create<Observable<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            List<(Subject<T> Subject, int Count)> openWindows = new();
            int totalCount = 0;
            IDisposable? upstream = null;

            Subject<T> firstSubject = new();
            openWindows.Add((firstSubject, 0));
            observer.OnNext(firstSubject);

            upstream = source.Subscribe(
                x =>
                {
                    Subject<T>? newWindowSubject = null;
                    List<Subject<T>>? toNotify = null;
                    List<Subject<T>>? toComplete = null;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        // Open a new window at every skip-th item (skipping the first which opened on subscribe)
                        if (totalCount > 0 && totalCount % skip == 0)
                        {
                            newWindowSubject = new Subject<T>();
                            openWindows.Add((newWindowSubject, 0));
                        }

                        // Snapshot all open windows for notification
                        toNotify = new List<Subject<T>>(openWindows.Count);
                        for (int i = 0; i < openWindows.Count; i++)
                        {
                            toNotify.Add(openWindows[i].Subject);
                        }

                        // Update counts and collect windows that are now full
                        List<Subject<T>>? closing = null;
                        for (int i = openWindows.Count - 1; i >= 0; i--)
                        {
                            var (subj, cnt) = openWindows[i];
                            int newCnt = cnt + 1;
                            if (newCnt >= count)
                            {
                                closing ??= new List<Subject<T>>();
                                closing.Add(subj);
                                openWindows.RemoveAt(i);
                            }
                            else
                            {
                                openWindows[i] = (subj, newCnt);
                            }
                        }

                        toComplete = closing;
                        totalCount++;
                    }

                    // Emit new window first so downstream can subscribe before it receives any value
                    if (newWindowSubject is not null)
                    {
                        observer.OnNext(newWindowSubject);
                    }

                    // Deliver value to all open windows (including the newly opened one)
                    foreach (Subject<T> w in toNotify!)
                    {
                        w.OnNext(x);
                    }

                    // Complete windows that have reached their capacity
                    if (toComplete is not null)
                    {
                        foreach (Subject<T> w in toComplete)
                        {
                            w.OnCompleted();
                        }
                    }
                },
                ex =>
                {
                    Subject<T>[]? windows;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        windows = openWindows.Select(w => w.Subject).ToArray();
                        openWindows.Clear();
                    }

                    foreach (Subject<T> w in windows)
                    {
                        w.OnCompleted();
                    }

                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    Subject<T>[]? windows;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        windows = openWindows.Select(w => w.Subject).ToArray();
                        openWindows.Clear();
                    }

                    foreach (Subject<T> w in windows)
                    {
                        w.OnCompleted();
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
                    openWindows.Clear();
                }
            });
        });
    }

    /// <summary>
    /// Projects each element of an observable sequence into consecutive non-overlapping windows
    /// that are produced based on timing information. A new window is opened every
    /// <paramref name="timeSpan"/> interval and the previous window is completed.
    /// </summary>
    public static Observable<Observable<T>> WindowTime<T>(this Observable<T> source, TimeSpan timeSpan, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (timeSpan <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSpan));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<Observable<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            Subject<T>? currentWindow = null;
            IDisposable? upstream = null;
            ITimer? timer = null;

            // Open the first window immediately
            currentWindow = new Subject<T>();
            observer.OnNext(currentWindow);

            timer = tp.CreateTimer(
                _ =>
                {
                    Subject<T>? windowToComplete;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        windowToComplete = currentWindow;
                        Subject<T> newWindow = new();
                        currentWindow = newWindow;

                        // Emit new window inside the lock so no source item can slip into the
                        // new window before a downstream subscriber has had a chance to subscribe.
                        observer.OnNext(newWindow);
                        timer!.Change(timeSpan, Timeout.InfiniteTimeSpan);
                    }

                    // Complete the old window after releasing the lock
                    windowToComplete?.OnCompleted();
                },
                null, timeSpan, Timeout.InfiniteTimeSpan);

            upstream = source.Subscribe(
                x =>
                {
                    Subject<T>? window;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        window = currentWindow;
                    }

                    window?.OnNext(x);
                },
                ex =>
                {
                    Subject<T>? window;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Dispose();
                        window = currentWindow;
                        currentWindow = null;
                    }

                    window?.OnCompleted();
                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    Subject<T>? window;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Dispose();
                        window = currentWindow;
                        currentWindow = null;
                    }

                    window?.OnCompleted();
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
                    currentWindow = null;
                }
            });
        });
    }

    /// <summary>
    /// Projects each element into consecutive windows that close when either
    /// <paramref name="timeSpan"/> elapses or <paramref name="maxCount"/> elements have been
    /// collected — whichever comes first.
    /// </summary>
    public static Observable<Observable<T>> WindowTime<T>(this Observable<T> source, TimeSpan timeSpan, int maxCount, TimeProvider? timeProvider = null)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (timeSpan <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSpan));
        }

        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        return Observable.Create<Observable<T>>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            Subject<T>? currentWindow = null;
            int windowItemCount = 0;
            IDisposable? upstream = null;
            ITimer? timer = null;

            void RollWindow(out Subject<T>? completed)
            {
                completed = currentWindow;
                Subject<T> newWindow = new();
                currentWindow = newWindow;
                windowItemCount = 0;
                observer.OnNext(newWindow);
                timer!.Change(timeSpan, Timeout.InfiniteTimeSpan);
            }

            currentWindow = new Subject<T>();
            observer.OnNext(currentWindow);

            timer = tp.CreateTimer(
                _ =>
                {
                    Subject<T>? windowToComplete;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        RollWindow(out windowToComplete);
                    }

                    windowToComplete?.OnCompleted();
                },
                null, timeSpan, Timeout.InfiniteTimeSpan);

            upstream = source.Subscribe(
                x =>
                {
                    Subject<T>? windowForItem;
                    Subject<T>? windowToComplete = null;

                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        windowForItem = currentWindow;
                        windowItemCount++;

                        if (windowItemCount >= maxCount)
                        {
                            RollWindow(out windowToComplete);
                        }
                    }

                    // Deliver item to the window that was current when it arrived
                    windowForItem?.OnNext(x);

                    // Close the count-saturated window after delivering its last item
                    windowToComplete?.OnCompleted();
                },
                ex =>
                {
                    Subject<T>? window;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Dispose();
                        window = currentWindow;
                        currentWindow = null;
                    }

                    window?.OnCompleted();
                    observer.OnErrorResume(ex);
                },
                r =>
                {
                    Subject<T>? window;
                    using (gate.EnterScope())
                    {
                        if (disposed)
                        {
                            return;
                        }

                        timer?.Dispose();
                        window = currentWindow;
                        currentWindow = null;
                    }

                    window?.OnCompleted();
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
                    currentWindow = null;
                }
            });
        });
    }
}
