// Convenience Convert operator for list change sets.
namespace R3.DynamicData.List;

public static partial class ObservableListEx
{
    /// <summary>
    /// Converts each item using the provided selector. Provided for API parity (synonym of Transform).
    /// </summary>
    public static Observable<IChangeSet<TDestination>> Convert<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, TDestination> selector)
        where TSource : notnull
        where TDestination : notnull
    {
        return source.Transform(selector);
    }
}
