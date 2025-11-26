using R3;

namespace R3Ext;

/// <summary>
/// Signal conversion extensions for R3 observables.
/// </summary>
public static class SignalExtensions
{
    /// <summary>
    /// Converts any upstream values to a Unit signal.
    /// Equivalent to Rx's AsSignal; maps to R3's AsUnitObservable.
    /// </summary>
    public static Observable<Unit> AsSignal<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source.AsUnitObservable();
    }
}
