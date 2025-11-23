using Microsoft.Extensions.Logging;
using R3;

namespace R3Ext;

/// <summary>
/// Observer and subscription helper extensions for R3.
/// </summary>
public static class ObserverExtensions
{
    /// <summary>
    /// Push multiple values to an observer.
    /// </summary>
    public static void OnNext<T>(this Observer<T> observer, params T[] values)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        if (values is null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            observer.OnNext(values[i]);
        }
    }

    /// <summary>
    /// Push a sequence of values to an observer.
    /// </summary>
    public static void OnNext<T>(this Observer<T> observer, IEnumerable<T> values)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        if (values is null)
        {
            return;
        }

        foreach (T v in values)
        {
            observer.OnNext(v);
        }
    }

    /// <summary>
    /// Partition a stream into two by predicate: matching and non-matching.
    /// </summary>
    public static (Observable<T> True, Observable<T> False) Partition<T>(this Observable<T> source, Func<T, bool> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        Observable<T> t = source.Where(predicate);
        Observable<T> f = source.Where(predicate, static (x, pred) => !pred(x));
        return (t, f);
    }

    /// <summary>
    /// Invoke action on subscription using R3's Do(onSubscribe:).
    /// </summary>
    public static Observable<T> DoOnSubscribe<T>(this Observable<T> source, Action onSubscribe)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (onSubscribe is null)
        {
            throw new ArgumentNullException(nameof(onSubscribe));
        }

        return source.Do(onSubscribe: onSubscribe);
    }

    /// <summary>
    /// Invoke action on dispose using R3's Do(onDispose:).
    /// </summary>
    public static Observable<T> DoOnDispose<T>(this Observable<T> source, Action onDispose)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (onDispose is null)
        {
            throw new ArgumentNullException(nameof(onDispose));
        }

        return source.Do(onDispose: onDispose);
    }

    public static Observable<T> Log<T>(this Observable<T> source, ILogger? logger = null, string? tag = null)
    {
        string prefix = string.IsNullOrWhiteSpace(tag) ? "R3Ext" : tag;

        if (logger != null)
        {
            return source.Do(
                x => logger.LogInformation("[{Prefix}] OnNext: {Value}", prefix, x),
                ex => logger.LogError(ex, "[{Prefix}] OnErrorResume: {Message}", prefix, ex.Message),
                r =>
                {
                    if (r.IsSuccess)
                    {
                        logger.LogInformation("[{Prefix}] OnCompleted: Success", prefix);
                    }
                    else
                    {
                        logger.LogError(r.Exception, "[{Prefix}] OnCompleted: {Message}", prefix, r.Exception?.Message);
                    }
                });
        }
        else
        {
            return source.Do(
                x => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnNext: {x}"),
                ex => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnErrorResume: {ex.Message}"),
                r => System.Diagnostics.Debug.WriteLine($"[{prefix}] OnCompleted: {(r.IsSuccess ? "Success" : r.Exception?.Message)}"));
        }
    }
}
