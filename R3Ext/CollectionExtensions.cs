using R3;

namespace R3Ext;

/// <summary>
/// Collection manipulation extensions for R3 observables.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Expand sequences emitted by the source into individual items.
    /// Optimized for arrays and IList to avoid iterator allocations.
    /// </summary>
    public static Observable<T> ForEach<T, TEnumerable>(this Observable<TEnumerable> source)
        where TEnumerable : IEnumerable<T>
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T, Observable<TEnumerable>>(source, static (observer, state) =>
        {
            return state.Subscribe(
                seq =>
                {
                    if (seq is null)
                    {
                        return;
                    }

                    // Fast path for arrays
                    if (seq is T[] arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            observer.OnNext(arr[i]);
                        }

                        return;
                    }

                    // Fast path for IList
                    if (seq is IList<T> list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            observer.OnNext(list[i]);
                        }

                        return;
                    }

                    foreach (T item in seq)
                    {
                        observer.OnNext(item);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Expand array items emitted by the source into individual items.
    /// </summary>
    public static Observable<T> ForEach<T>(this Observable<T[]> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T, Observable<T[]>>(source, static (observer, state) =>
        {
            return state.Subscribe(
                arr =>
                {
                    if (arr is null)
                    {
                        return;
                    }

                    for (int i = 0; i < arr.Length; i++)
                    {
                        observer.OnNext(arr[i]);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Expand IList items emitted by the source into individual items.
    /// </summary>
    public static Observable<T> ForEach<T>(this Observable<IList<T>> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T, Observable<IList<T>>>(source, static (observer, state) =>
        {
            return state.Subscribe(
                list =>
                {
                    if (list is null)
                    {
                        return;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        observer.OnNext(list[i]);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Expand List items emitted by the source into individual items.
    /// </summary>
    public static Observable<T> ForEach<T>(this Observable<List<T>> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T, Observable<List<T>>>(source, static (observer, state) =>
        {
            return state.Subscribe(
                list =>
                {
                    if (list is null)
                    {
                        return;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        observer.OnNext(list[i]);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// In-place Fisher-Yates shuffle for lists.
    /// </summary>
    public static void Shuffle<T>(this IList<T> list, Random? rng = null)
    {
        if (list is null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        rng ??= Random.Shared;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// In-place Fisher-Yates shuffle for arrays.
    /// </summary>
    public static void Shuffle<T>(this T[] array, Random? rng = null)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        ((IList<T>)array).Shuffle(rng);
    }
}
