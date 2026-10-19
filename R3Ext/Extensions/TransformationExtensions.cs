using R3;

namespace R3Ext;

/// <summary>
/// Transformation extensions for R3 observables.
/// </summary>
public static class TransformationExtensions
{
    /// <summary>
    /// Projects each element to a constant value, replacing all upstream values.
    /// </summary>
    public static Observable<TResult> MapTo<T, TResult>(this Observable<T> source, TResult constant)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(_ => constant);
    }

    /// <summary>
    /// Applies a selector to each element and filters out null results (reference type variant).
    /// </summary>
    public static Observable<TResult> CompactMap<T, TResult>(this Observable<T> source, Func<T, TResult?> selector)
        where TResult : class
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.Select(selector).Where(x => x is not null).Select(x => x!);
    }

    /// <summary>
    /// Applies a selector to each element and filters out null results (value type variant).
    /// </summary>
    public static Observable<TResult> CompactMap<T, TResult>(this Observable<T> source, Func<T, TResult?> selector)
        where TResult : struct
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        return source.Select(selector).Where(x => x.HasValue).Select(x => x!.Value);
    }

    /// <summary>
    /// Pairs each element with its zero-based index in the sequence.
    /// </summary>
    public static Observable<(T Value, int Index)> WithIndex<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<(T Value, int Index)>(observer =>
        {
            Lock gate = new();
            bool disposed = false;
            int index = 0;
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

                        observer.OnNext((x, index++));
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
    /// Applies an accumulator function over the source sequence, emitting each intermediate result.
    /// Equivalent to <c>Scan</c> with a seed value.
    /// </summary>
    public static Observable<TAccumulate> RunningFold<T, TAccumulate>(
        this Observable<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> accumulator)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (accumulator is null)
        {
            throw new ArgumentNullException(nameof(accumulator));
        }

        return source.Scan(seed, accumulator);
    }

    /// <summary>
    /// Applies an accumulator function over the source sequence without a seed, emitting each intermediate result.
    /// Equivalent to <c>Scan</c> without a seed.
    /// </summary>
    public static Observable<T> RunningReduce<T>(this Observable<T> source, Func<T, T, T> accumulator)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (accumulator is null)
        {
            throw new ArgumentNullException(nameof(accumulator));
        }

        return source.Scan(accumulator);
    }
}
