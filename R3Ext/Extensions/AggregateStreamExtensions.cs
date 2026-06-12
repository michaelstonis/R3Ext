using System.Numerics;
using R3;

namespace R3Ext;

/// <summary>
/// Stream aggregate operators that emit incremental aggregate values as each element arrives.
/// </summary>
public static class AggregateStreamExtensions
{
    /// <summary>
    /// Emits the count of items received so far.
    /// </summary>
    public static Observable<int> RunningCount<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<int>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int count = 0;
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

                        count++;
                        observer.OnNext(count);
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
    /// Emits the running sum using C# generic math.
    /// </summary>
    public static Observable<T> RunningSum<T>(this Observable<T> source)
        where T : INumber<T>
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Scan(T.Zero, (acc, x) => acc + x);
    }

    /// <summary>
    /// Emits the running average of a <see cref="double"/> stream.
    /// </summary>
    public static Observable<double> RunningAverage(this Observable<double> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<double>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            double sum = 0d;
            int count = 0;
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

                        sum += x;
                        count++;
                        observer.OnNext(sum / count);
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
    /// Emits the running average of a <see cref="float"/> stream as <see cref="double"/>.
    /// </summary>
    public static Observable<double> RunningAverage(this Observable<float> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<double>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            double sum = 0d;
            int count = 0;
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

                        sum += x;
                        count++;
                        observer.OnNext(sum / count);
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
    /// Emits the running average of a <see cref="decimal"/> stream.
    /// </summary>
    public static Observable<decimal> RunningAverage(this Observable<decimal> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<decimal>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            decimal sum = 0m;
            int count = 0;
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

                        sum += x;
                        count++;
                        observer.OnNext(sum / count);
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
    /// Emits the running average of an <see cref="int"/> stream as <see cref="double"/>.
    /// </summary>
    public static Observable<double> RunningAverage(this Observable<int> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<double>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            double sum = 0d;
            int count = 0;
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

                        sum += x;
                        count++;
                        observer.OnNext(sum / count);
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
    /// Emits the running minimum value seen so far.
    /// </summary>
    public static Observable<T> RunningMin<T>(this Observable<T> source)
        where T : IComparable<T>
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? min = default;
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

                        if (!hasValue || x.CompareTo(min) < 0)
                        {
                            min = x;
                            hasValue = true;
                        }

                        observer.OnNext(min!);
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
    /// Emits the running minimum value seen so far using a custom comparer.
    /// </summary>
    public static Observable<T> RunningMin<T>(this Observable<T> source, IComparer<T> comparer)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (comparer is null)
        {
            throw new ArgumentNullException(nameof(comparer));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? min = default;
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

                        if (!hasValue || comparer.Compare(x, min) < 0)
                        {
                            min = x;
                            hasValue = true;
                        }

                        observer.OnNext(min!);
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
    /// Emits the running maximum value seen so far.
    /// </summary>
    public static Observable<T> RunningMax<T>(this Observable<T> source)
        where T : IComparable<T>
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? max = default;
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

                        if (!hasValue || x.CompareTo(max) > 0)
                        {
                            max = x;
                            hasValue = true;
                        }

                        observer.OnNext(max!);
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
    /// Emits the running maximum value seen so far using a custom comparer.
    /// </summary>
    public static Observable<T> RunningMax<T>(this Observable<T> source, IComparer<T> comparer)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (comparer is null)
        {
            throw new ArgumentNullException(nameof(comparer));
        }

        return Observable.Create<T>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            T? max = default;
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

                        if (!hasValue || comparer.Compare(x, max) > 0)
                        {
                            max = x;
                            hasValue = true;
                        }

                        observer.OnNext(max!);
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
