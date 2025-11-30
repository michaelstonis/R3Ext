using R3;
using R3Ext.Activation;

namespace R3Ext.SampleApp.ViewModels;

/// <summary>
/// ViewModel demonstrating the activation and attachment lifecycle patterns.
/// Implements <see cref="IActivatableViewModel"/> for visibility-based activation
/// and <see cref="IAttachableViewModel"/> for load-based attachment.
/// </summary>
public sealed class ActivationDemoViewModel : IActivatableViewModel, IAttachableViewModel, IDisposable
{
    private readonly ReactiveProperty<string> _activationStatus = new("Not Activated");
    private readonly ReactiveProperty<int> _activationCount = new(0);
    private readonly ReactiveProperty<int> _deactivationCount = new(0);
    private readonly ReactiveProperty<string> _timerValue = new("--:--");
    private readonly ReactiveProperty<bool> _isTimerRunning = new(false);
    private readonly ReactiveProperty<string> _attachmentStatus = new("Not Attached");
    private readonly ReactiveProperty<int> _attachmentCount = new(0);
    private readonly ReactiveProperty<int> _detachmentCount = new(0);
    private DisposableBag _viewModelDisposables;

    public ActivationDemoViewModel()
    {
        // Set up WhenActivated on the ViewModel itself (visibility-based)
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            // Track activation
            _activationCount.Value++;
            _activationStatus.Value = $"Activated at {DateTime.Now:HH:mm:ss}";
            _isTimerRunning.Value = true;

            // Subscribe to a timer that only runs while activated
            var startTime = DateTime.Now;
            Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    var elapsed = DateTime.Now - startTime;
                    var minutes = (int)elapsed.TotalMinutes;
                    var seconds = elapsed.Seconds;
                    _timerValue.Value = $"{minutes:D2}:{seconds:D2}";
                })
                .AddTo(ref disposables);

            // When this block completes (deactivation), cleanup will happen automatically
            Disposable.Create(() =>
            {
                _deactivationCount.Value++;
                _activationStatus.Value = $"Deactivated at {DateTime.Now:HH:mm:ss}";
                _isTimerRunning.Value = false;
                _timerValue.Value = "--:--";
            }).AddTo(ref disposables);
        }).AddTo(ref _viewModelDisposables);

        // Set up WhenAttached on the ViewModel itself (load-based)
        this.WhenAttached((ref DisposableBag disposables) =>
        {
            // Track attachment
            _attachmentCount.Value++;
            _attachmentStatus.Value = $"Attached at {DateTime.Now:HH:mm:ss}";

            // When this block completes (detachment), cleanup will happen automatically
            Disposable.Create(() =>
            {
                _detachmentCount.Value++;
                _attachmentStatus.Value = $"Detached at {DateTime.Now:HH:mm:ss}";
            }).AddTo(ref disposables);
        }).AddTo(ref _viewModelDisposables);
    }

    /// <summary>
    /// Gets the ViewModel activator that manages the activation lifecycle (visibility-based).
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Gets the ViewModel attacher that manages the attachment lifecycle (load-based).
    /// </summary>
    public ViewModelAttacher Attacher { get; } = new();

    /// <summary>
    /// Gets the current activation status message.
    /// </summary>
    public ReadOnlyReactiveProperty<string> ActivationStatus => _activationStatus;

    /// <summary>
    /// Gets the number of times the ViewModel has been activated.
    /// </summary>
    public ReadOnlyReactiveProperty<int> ActivationCount => _activationCount;

    /// <summary>
    /// Gets the number of times the ViewModel has been deactivated.
    /// </summary>
    public ReadOnlyReactiveProperty<int> DeactivationCount => _deactivationCount;

    /// <summary>
    /// Gets the current timer value (only runs while activated).
    /// </summary>
    public ReadOnlyReactiveProperty<string> TimerValue => _timerValue;

    /// <summary>
    /// Gets whether the timer is currently running.
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsTimerRunning => _isTimerRunning;

    /// <summary>
    /// Gets the current attachment status message.
    /// </summary>
    public ReadOnlyReactiveProperty<string> AttachmentStatus => _attachmentStatus;

    /// <summary>
    /// Gets the number of times the ViewModel has been attached.
    /// </summary>
    public ReadOnlyReactiveProperty<int> AttachmentCount => _attachmentCount;

    /// <summary>
    /// Gets the number of times the ViewModel has been detached.
    /// </summary>
    public ReadOnlyReactiveProperty<int> DetachmentCount => _detachmentCount;

    public void Dispose()
    {
        _viewModelDisposables.Dispose();
        _activationStatus.Dispose();
        _activationCount.Dispose();
        _deactivationCount.Dispose();
        _timerValue.Dispose();
        _isTimerRunning.Dispose();
        _attachmentStatus.Dispose();
        _attachmentCount.Dispose();
        _detachmentCount.Dispose();
    }
}
