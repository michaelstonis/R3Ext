using System;
using System.Diagnostics.CodeAnalysis;
using R3;

namespace R3Ext;

/// <summary>
/// Extensions ported/adapted from ReactiveUI/Extensions for R3.
/// Each method is committed individually per migration requirement.
/// </summary>
public static class ReactivePortedExtensions
{
    /// <summary>
    /// Converts any upstream values to a Unit signal.
    /// Equivalent to Rx's AsSignal; maps to R3's AsUnitObservable.
    /// </summary>
    public static Observable<Unit> AsSignal<T>(this Observable<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.AsUnitObservable();
    }

    /// <summary>
    /// Logical NOT for boolean streams.
    /// </summary>
    public static Observable<bool> Not(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Select(x => !x);
    }

    /// <summary>
    /// Filters a boolean stream to only true values.
    /// </summary>
    public static Observable<bool> WhereTrue(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Where(x => x);
    }

    /// <summary>
    /// Filters a boolean stream to only false values.
    /// </summary>
    public static Observable<bool> WhereFalse(this Observable<bool> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Where(x => !x);
    }

    /// <summary>
    /// Filters out null values for nullable reference types and casts to non-nullable.
    /// </summary>
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source) where T : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
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
    public static Observable<T> WhereIsNotNull<T>(this Observable<T?> source) where T : struct
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
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
    /// DebounceImmediate: emits the first item immediately, then debounces subsequent items.
    /// Equivalent to combining Take(1) with Debounce for the remainder of the stream.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="source">Upstream observable.</param>
    /// <param name="dueTime">Debounce window applied after first emission.</param>
    /// <param name="timeProvider">Optional TimeProvider; defaults to ObservableSystem.DefaultTimeProvider.</param>
    /// <returns>An observable that emits first value immediately then debounces subsequent values.</returns>
    public static Observable<T> DebounceImmediate<T>(this Observable<T> source, TimeSpan dueTime, TimeProvider? timeProvider = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (dueTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(dueTime));
        var tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        // Share subscription to avoid multiple subscriptions to source.
        var shared = source.Share();
        var first = shared.Take(1);
        var rest = shared.Skip(1).Debounce(dueTime, tp);
        return Observable.Merge(first, rest);
    }
}
