using System;
using System.Linq;
using R3;

namespace R3Ext;

/// <summary>
/// Combining operation extensions for R3 observables.
/// </summary>
public static class CombineExtensions
{
    /// <summary>
    /// Returns true when all latest values are true across the provided boolean observables.
    /// </summary>
    public static Observable<bool> CombineLatestValuesAreAllTrue(this System.Collections.Generic.IEnumerable<Observable<bool>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var list = sources as System.Collections.Generic.IList<Observable<bool>> ?? new System.Collections.Generic.List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            var all = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i]) { all = false; break; }
            }
            return all;
        });
    }

    /// <summary>
    /// Returns true when all latest values are false across the provided boolean observables.
    /// </summary>
    public static Observable<bool> CombineLatestValuesAreAllFalse(this System.Collections.Generic.IEnumerable<Observable<bool>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var list = sources as System.Collections.Generic.IList<Observable<bool>> ?? new System.Collections.Generic.List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            var allFalse = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i]) { allFalse = false; break; }
            }
            return allFalse;
        });
    }
}
