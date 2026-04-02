using R3;

namespace R3Ext;

/// <summary>
/// Wraps any <see cref="Observable{T}"/> to expose only the read-only observable surface,
/// hiding <c>OnNext</c>, <c>OnCompleted</c>, and other subject methods.
/// </summary>
public sealed class ReadOnlySubject<T> : Observable<T>
{
    private readonly Observable<T> _inner;

    /// <param name="inner">The source observable to wrap.</param>
    public ReadOnlySubject(Observable<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc/>
    protected override IDisposable SubscribeCore(Observer<T> observer)
        => _inner.Subscribe(observer.OnNext, observer.OnErrorResume, observer.OnCompleted);
}

/// <summary>
/// Extension methods for wrapping subjects as read-only observables.
/// </summary>
public static class SubjectExtensions
{
    /// <summary>Wraps any observable as a <see cref="ReadOnlySubject{T}"/>.</summary>
    public static ReadOnlySubject<T> AsReadOnly<T>(this Observable<T> source)
        => new ReadOnlySubject<T>(source);

    /// <summary>Wraps a <see cref="Subject{T}"/> as a <see cref="ReadOnlySubject{T}"/>.</summary>
    public static ReadOnlySubject<T> AsReadOnly<T>(this Subject<T> subject)
        => new ReadOnlySubject<T>(subject);

    /// <summary>Wraps a <see cref="BehaviorSubject{T}"/> as a <see cref="ReadOnlySubject{T}"/>.</summary>
    public static ReadOnlySubject<T> AsReadOnly<T>(this BehaviorSubject<T> subject)
        => new ReadOnlySubject<T>(subject);
}
