using R3;

namespace R3Ext;

public static partial class ErrorHandlingExtensions
{
    /// <summary>
    /// Ignore errors from the source and complete instead.
    /// </summary>
    public static Observable<T> CatchIgnore<T>(this Observable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
            .OnErrorResumeAsFailure()
            .Catch(Observable.Empty<T>());
    }

    /// <summary>
    /// Replace an error with a single fallback value, then complete.
    /// </summary>
    public static Observable<T> CatchAndReturn<T>(this Observable<T> source, T value)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
            .OnErrorResumeAsFailure()
            .Catch(Observable.Return(value));
    }
}
