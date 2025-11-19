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
    public static Observable<bool> CombineLatestValuesAreAllTrue(this IEnumerable<Observable<bool>> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        IList<Observable<bool>> list = sources as IList<Observable<bool>> ?? new List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            bool all = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i])
                {
                    all = false;
                    break;
                }
            }

            return all;
        });
    }

    /// <summary>
    /// Returns true when all latest values are false across the provided boolean observables.
    /// </summary>
    public static Observable<bool> CombineLatestValuesAreAllFalse(this IEnumerable<Observable<bool>> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        IList<Observable<bool>> list = sources as IList<Observable<bool>> ?? new List<Observable<bool>>(sources);
        return Observable.CombineLatest(list).Select(values =>
        {
            bool allFalse = true;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                {
                    allFalse = false;
                    break;
                }
            }

            return allFalse;
        });
    }
}
