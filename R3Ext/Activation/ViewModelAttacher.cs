using R3;

namespace R3Ext.Activation;

/// <summary>
/// Manages the attachment lifecycle for a view model.
/// Create an instance of this class in your view model to enable attachment support.
/// </summary>
/// <remarks>
/// <para>
/// When a view implementing <see cref="IViewFor{TViewModel}"/> is loaded/unloaded,
/// the associated view model's attacher is automatically triggered (unless opted out).
/// </para>
/// <para>
/// This differs from <see cref="ViewModelActivator"/> in that it responds to
/// Loaded/Unloaded events rather than Appearing/Disappearing events.
/// </para>
/// <para>
/// This class is AOT-compatible and uses no reflection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyViewModel : IAttachableViewModel
/// {
///     public ViewModelAttacher Attacher { get; } = new();
///
///     public MyViewModel()
///     {
///         this.WhenAttached(disposables =&gt;
///         {
///             // Set up subscriptions that should live only while attached
///             someObservable
///                 .Subscribe(...)
///                 .AddTo(disposables);
///         });
///     }
/// }
/// </code>
/// </example>
public sealed class ViewModelAttacher : IDisposable
{
    private readonly Subject<ActivationState> _attachmentSubject = new();
    private readonly object _lock = new();
    private int _attachmentCount;
    private bool _isDisposed;

    /// <summary>
    /// Gets an observable that emits when the view model is attached or detached.
    /// </summary>
    public Observable<ActivationState> Activation => _attachmentSubject;

    /// <summary>
    /// Gets a value indicating whether the view model is currently attached.
    /// </summary>
    public bool IsAttached => _attachmentCount > 0;

    /// <summary>
    /// Attaches the view model. Call this when the associated view is loaded.
    /// Multiple calls are reference-counted; the view model stays attached until
    /// all attachments are balanced by detachments.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that detaches the view model when disposed.</returns>
    public IDisposable Attach()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return Disposable.Empty;
            }

            _attachmentCount++;
            if (_attachmentCount == 1)
            {
                _attachmentSubject.OnNext(ActivationState.Activated);
            }
        }

        return new AttachmentHandle(this);
    }

    /// <summary>
    /// Detaches the view model. Called when the associated view is unloaded.
    /// </summary>
    public void Detach()
    {
        lock (_lock)
        {
            if (_isDisposed || _attachmentCount == 0)
            {
                return;
            }

            _attachmentCount--;
            if (_attachmentCount == 0)
            {
                _attachmentSubject.OnNext(ActivationState.Deactivated);
            }
        }
    }

    /// <summary>
    /// Disposes the attacher and completes the attachment observable.
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

            if (_attachmentCount > 0)
            {
                _attachmentSubject.OnNext(ActivationState.Deactivated);
                _attachmentCount = 0;
            }

            _attachmentSubject.OnCompleted();
            _attachmentSubject.Dispose();
        }
    }

    /// <summary>
    /// Handle that detaches the view model when disposed.
    /// </summary>
    private sealed class AttachmentHandle : IDisposable
    {
        private ViewModelAttacher? _attacher;

        public AttachmentHandle(ViewModelAttacher attacher)
        {
            _attacher = attacher;
        }

        public void Dispose()
        {
            var attacher = Interlocked.Exchange(ref _attacher, null);
            attacher?.Detach();
        }
    }
}
