using R3;

namespace R3Ext;

/// <summary>
/// A Subject that buffers the last value and emits it to all current and future subscribers only when
/// <see cref="OnCompleted()"/> is called with a successful result. On failure, no value is emitted.
/// </summary>
public sealed class AsyncSubject<T> : Observable<T>, IDisposable
{
    private readonly Lock _gate = new();
    private bool _isCompleted = false;
    private Result _completionResult = default;
    private T? _lastValue = default;
    private bool _hasValue = false;
    private List<Observer<T>> _observers = new();
    private bool _disposed = false;

    /// <summary>
    /// Buffers <paramref name="value"/> as the latest value. Does not emit to subscribers until completion.
    /// </summary>
    public void OnNext(T value)
    {
        using (_gate.EnterScope())
        {
            if (_disposed || _isCompleted)
            {
                return;
            }

            _lastValue = value;
            _hasValue = true;
        }
    }

    /// <summary>
    /// Forwards the error to all current subscribers without stopping value buffering.
    /// </summary>
    public void OnErrorResume(Exception error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        Observer<T>[] observers;
        using (_gate.EnterScope())
        {
            if (_disposed || _isCompleted)
            {
                return;
            }

            observers = _observers.ToArray();
        }

        foreach (var obs in observers)
        {
            obs.OnErrorResume(error);
        }
    }

    /// <summary>Completes with a successful result, emitting the last buffered value (if any).</summary>
    public void OnCompleted() => OnCompleted(Result.Success);

    /// <summary>Completes with the given <paramref name="exception"/> as a failure result (no value emitted).</summary>
    public void OnCompleted(Exception exception) => OnCompleted(Result.Failure(exception));

    /// <summary>Completes with the given <paramref name="result"/>.</summary>
    public void OnCompleted(Result result)
    {
        Observer<T>[] observers;
        T? lastValue;
        bool hasValue;
        using (_gate.EnterScope())
        {
            if (_disposed || _isCompleted)
            {
                return;
            }

            _isCompleted = true;
            _completionResult = result;
            lastValue = _lastValue;
            hasValue = _hasValue;
            observers = _observers.ToArray();
            _observers.Clear();
        }

        foreach (var obs in observers)
        {
            if (result.IsSuccess && hasValue)
            {
                obs.OnNext(lastValue!);
            }

            obs.OnCompleted(result);
        }
    }

    /// <inheritdoc/>
    protected override IDisposable SubscribeCore(Observer<T> observer)
    {
        using (_gate.EnterScope())
        {
            if (_disposed)
            {
                observer.OnCompleted(Result.Failure(new ObjectDisposedException(nameof(AsyncSubject<T>))));
                return Disposable.Empty;
            }

            if (_isCompleted)
            {
                if (_completionResult.IsSuccess && _hasValue)
                {
                    observer.OnNext(_lastValue!);
                }

                observer.OnCompleted(_completionResult);
                return Disposable.Empty;
            }

            _observers.Add(observer);
        }

        return Disposable.Create(() =>
        {
            using (_gate.EnterScope())
            {
                _observers.Remove(observer);
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        using (_gate.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _observers.Clear();
        }
    }
}
