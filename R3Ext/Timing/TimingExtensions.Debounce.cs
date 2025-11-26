using R3;

namespace R3Ext;

public static partial class TimingExtensions
{
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
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (dueTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueTime));
        }

        TimeProvider tp = timeProvider ?? ObservableSystem.DefaultTimeProvider;

        // Share subscription to avoid multiple subscriptions to source.
        Observable<T> shared = source.Share();
        Observable<T> first = shared.Take(1);
        Observable<T> rest = shared.Skip(1).Debounce(dueTime, tp);
        return Observable.Merge(first, rest);
    }
}
