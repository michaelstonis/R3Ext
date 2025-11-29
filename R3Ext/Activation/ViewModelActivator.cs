using R3;

namespace R3Ext.Activation;

/// <summary>
/// Manages the activation lifecycle for a view model.
/// Create an instance of this class in your view model to enable activation support.
/// </summary>
/// <remarks>
/// <para>
/// When a view implementing <see cref="IViewFor{TViewModel}"/> activates/deactivates,
/// the associated view model's activator is automatically triggered (unless opted out).
/// </para>
/// <para>
/// This class is AOT-compatible and uses no reflection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyViewModel : IActivatableViewModel
/// {
///     public ViewModelActivator Activator { get; } = new();
///
///     public MyViewModel()
///     {
///         this.WhenActivated(disposables =&gt;
///         {
///             // Set up subscriptions that should live only while activated
///             someObservable
///                 .Subscribe(...)
///                 .AddTo(disposables);
///         });
///     }
/// }
/// </code>
/// </example>
public sealed class ViewModelActivator : IDisposable
{
    private readonly Subject<ActivationState> _activationSubject = new();
    private readonly object _lock = new();
    private int _activationCount;
    private bool _isDisposed;

    /// <summary>
    /// Gets an observable that emits when the view model is activated or deactivated.
    /// </summary>
    public Observable<ActivationState> Activation => _activationSubject;

    /// <summary>
    /// Gets a value indicating whether the view model is currently activated.
    /// </summary>
    public bool IsActivated => _activationCount > 0;

    /// <summary>
    /// Activates the view model. Call this when the associated view becomes active.
    /// Multiple calls are reference-counted; the view model stays activated until
    /// all activations are balanced by deactivations.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that deactivates the view model when disposed.</returns>
    public IDisposable Activate()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return Disposable.Empty;
            }

            _activationCount++;
            if (_activationCount == 1)
            {
                _activationSubject.OnNext(ActivationState.Activated);
            }
        }

        return new ActivationHandle(this);
    }

    /// <summary>
    /// Deactivates the view model. Called when the associated view becomes inactive.
    /// </summary>
    public void Deactivate()
    {
        lock (_lock)
        {
            if (_isDisposed || _activationCount == 0)
            {
                return;
            }

            _activationCount--;
            if (_activationCount == 0)
            {
                _activationSubject.OnNext(ActivationState.Deactivated);
            }
        }
    }

    /// <summary>
    /// Disposes the activator and completes the activation observable.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _activationCount = 0;
        }

        _activationSubject.OnCompleted();
        _activationSubject.Dispose();
    }

    /// <summary>
    /// Handle that deactivates the view model when disposed.
    /// </summary>
    private sealed class ActivationHandle(ViewModelActivator activator) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                activator.Deactivate();
            }
        }
    }
}
