// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using R3;
using R3Ext.Activation;

namespace R3Ext.SampleApp.ViewModels;

/// <summary>
/// Demonstrates the activation lifecycle with IActivatableViewModel and IAttachableViewModel.
/// Uses ReactiveProperty for cross-platform reactive bindings.
/// </summary>
public sealed class ActivationDemoViewModel : IActivatableViewModel, IAttachableViewModel, IDisposable
{
    private readonly ReactiveProperty<string> _activationStatus = new("Inactive");
    private readonly ReactiveProperty<string> _attachmentStatus = new("Detached");
    private readonly ReactiveProperty<int> _activationCount = new(0);
    private readonly ReactiveProperty<int> _deactivationCount = new(0);
    private readonly ReactiveProperty<int> _attachmentCount = new(0);
    private readonly ReactiveProperty<int> _detachmentCount = new(0);
    private readonly ReactiveProperty<int> _timerValue = new(0);
    private readonly ReactiveProperty<string> _timerDisplay = new("00:00");
    private readonly ReactiveProperty<bool> _isTimerRunning = new(false);
    private DisposableBag _viewModelDisposables;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivationDemoViewModel"/> class.
    /// </summary>
    public ActivationDemoViewModel()
    {
        // Setup WhenActivated - timer runs only while activated
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            _activationCount.Value++;
            _activationStatus.Value = "Active";
            _isTimerRunning.Value = true;

            // Start a timer that runs only while activated
            var startTime = DateTime.Now;
            Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    _timerValue.Value++;
                    var elapsed = DateTime.Now - startTime;
                    var minutes = (int)elapsed.TotalMinutes;
                    var seconds = elapsed.Seconds;
                    _timerDisplay.Value = $"{minutes:D2}:{seconds:D2}";
                })
                .AddTo(ref disposables);

            // Cleanup when deactivated
            Disposable.Create(() =>
            {
                _deactivationCount.Value++;
                _activationStatus.Value = "Inactive";
                _isTimerRunning.Value = false;
            }).AddTo(ref disposables);
        }).AddTo(ref _viewModelDisposables);

        // Setup WhenAttached - lightweight attachment tracking
        this.WhenAttached((ref DisposableBag disposables) =>
        {
            _attachmentCount.Value++;
            _attachmentStatus.Value = "Attached";

            Disposable.Create(() =>
            {
                _detachmentCount.Value++;
                _attachmentStatus.Value = "Detached";
            }).AddTo(ref disposables);
        }).AddTo(ref _viewModelDisposables);
    }

    /// <summary>
    /// Gets the activator that manages the view model's activation state.
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Gets the attacher that manages the view model's attachment state.
    /// </summary>
    public ViewModelAttacher Attacher { get; } = new();

    /// <summary>
    /// Gets the current activation status.
    /// </summary>
    public ReadOnlyReactiveProperty<string> ActivationStatus => _activationStatus;

    /// <summary>
    /// Gets the current attachment status.
    /// </summary>
    public ReadOnlyReactiveProperty<string> AttachmentStatus => _attachmentStatus;

    /// <summary>
    /// Gets the number of times the view model has been activated.
    /// </summary>
    public ReadOnlyReactiveProperty<int> ActivationCount => _activationCount;

    /// <summary>
    /// Gets the number of times the view model has been deactivated.
    /// </summary>
    public ReadOnlyReactiveProperty<int> DeactivationCount => _deactivationCount;

    /// <summary>
    /// Gets the number of times the view model has been attached.
    /// </summary>
    public ReadOnlyReactiveProperty<int> AttachmentCount => _attachmentCount;

    /// <summary>
    /// Gets the number of times the view model has been detached.
    /// </summary>
    public ReadOnlyReactiveProperty<int> DetachmentCount => _detachmentCount;

    /// <summary>
    /// Gets the timer value (increments each second while active).
    /// </summary>
    public ReadOnlyReactiveProperty<int> TimerValue => _timerValue;

    /// <summary>
    /// Gets the timer display string (MM:SS format).
    /// </summary>
    public ReadOnlyReactiveProperty<string> TimerDisplay => _timerDisplay;

    /// <summary>
    /// Gets whether the timer is currently running.
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsTimerRunning => _isTimerRunning;

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelDisposables.Dispose();
        _activationStatus.Dispose();
        _attachmentStatus.Dispose();
        _activationCount.Dispose();
        _deactivationCount.Dispose();
        _attachmentCount.Dispose();
        _detachmentCount.Dispose();
        _timerValue.Dispose();
        _timerDisplay.Dispose();
        _isTimerRunning.Dispose();
    }
}
