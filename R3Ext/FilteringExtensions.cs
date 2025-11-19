using System.Text.RegularExpressions;
using R3;

namespace R3Ext;

/// <summary>
/// Filtering and conditional extensions for R3 observables.
/// </summary>
public static class FilteringExtensions
{
    /// <summary>
    /// Logical NOT for boolean streams.
    /// </summary>
    public static Observable<bool> Not(this Observable<bool> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Select(x => !x);
    }

    /// <summary>
    /// Filters a boolean stream to only true values.
    /// </summary>
    public static Observable<bool> WhereTrue(this Observable<bool> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(x => x);
    }

    /// <summary>
    /// Filters a boolean stream to only false values.
    /// </summary>
    public static Observable<bool> WhereFalse(this Observable<bool> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.Where(x => !x);
    }

    /// <summary>
    /// Filters out null values for nullable reference types and casts to non-nullable.
    /// </summary>
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source)
        where T : class
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            return source.Subscribe(
                x =>
                {
                    if (x is not null)
                    {
                        observer.OnNext(x);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Filters out null values for nullable value types and casts to non-nullable.
    /// </summary>
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source)
        where T : struct
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return Observable.Create<T>(observer =>
        {
            return source.Subscribe(
                x =>
                {
                    if (x.HasValue)
                    {
                        observer.OnNext(x.Value);
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Emit the first value matching the predicate, then complete.
    /// </summary>
    public static Observable<T> WaitUntil<T>(this Observable<T> source, Func<T, bool> predicate)
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
    /// Take values until predicate matches, including the matching value, then complete.
    /// </summary>
    public static Observable<T> TakeUntil<T>(this Observable<T> source, Func<T, bool> predicate)
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
            return source.Subscribe(
                x =>
                {
                    observer.OnNext(x);
                    if (predicate(x))
                    {
                        observer.OnCompleted();
                    }
                },
                observer.OnErrorResume,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Filter string values by a regular expression pattern.
    /// </summary>
    public static Observable<string> Filter(this Observable<string> source, string pattern,
        System.Text.RegularExpressions.RegexOptions options = System.Text.RegularExpressions.RegexOptions.None)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (pattern is null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        Regex regex = new(pattern, options);
        return source.Where(s => s != null && regex.IsMatch(s));
    }

    /// <summary>
    /// Repeats the source sequence while the condition evaluates to true.
    /// </summary>
    public static Observable<T> While<T>(this Observable<T> source, Func<bool> condition)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        return Observable.Defer(() =>
        {
            if (condition())
            {
                // Concat source then recurse via Defer
                return Observable.Concat(source, Observable.Defer(() => source.While(condition)));
            }

            return Observable.Empty<T>();
        });
    }
}
