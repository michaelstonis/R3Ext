// Convenience Convert operator for list change sets.
namespace R3.DynamicData.List;

/// <summary>
/// Extension methods for observable list change sets.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Converts each item using the provided selector. Provided for API parity (synonym of Transform).
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source observable list.</param>
    /// <param name="selector">Function to convert each source item to a destination item.</param>
    /// <returns>An observable that emits change sets with converted items.</returns>
    public static Observable<IChangeSet<TDestination>> Convert<TSource, TDestination>(
        this Observable<IChangeSet<TSource>> source,
        Func<TSource, TDestination> selector)
        where TSource : notnull
        where TDestination : notnull
    {
        return source.Transform(selector);
    }
}
