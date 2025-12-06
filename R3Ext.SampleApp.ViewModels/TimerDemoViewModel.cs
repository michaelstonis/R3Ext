// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using R3;
using R3Ext.Activation;

namespace R3Ext.SampleApp.ViewModels;

/// <summary>
/// Demonstrates a timer that runs while the view is active.
/// Uses ReactiveProperty for cross-platform reactive bindings.
/// </summary>
public sealed class TimerDemoViewModel : IActivatableViewModel, IDisposable
{
    private readonly ReactiveProperty<string> _status = new("Waiting...");
    private readonly ReactiveProperty<int> _elapsedSeconds = new(0);
    private readonly ReactiveProperty<string> _elapsedDisplay = new("0");
    private readonly ReactiveProperty<bool> _isRunning = new(false);
    private DisposableBag _viewModelDisposables;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerDemoViewModel"/> class.
    /// </summary>
    public TimerDemoViewModel()
    {
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            _status.Value = "Timer running!";
            _isRunning.Value = true;

            Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    _elapsedSeconds.Value++;
                    _elapsedDisplay.Value = _elapsedSeconds.Value.ToString();
                })
                .AddTo(ref disposables);

            Disposable.Create(() =>
            {
                _status.Value = "Timer paused";
                _isRunning.Value = false;
            }).AddTo(ref disposables);
        }).AddTo(ref _viewModelDisposables);
    }

    /// <summary>
    /// Gets the activator that manages the view model's activation state.
    /// </summary>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Gets the current timer status.
    /// </summary>
    public ReadOnlyReactiveProperty<string> Status => _status;

    /// <summary>
    /// Gets the elapsed seconds.
    /// </summary>
    public ReadOnlyReactiveProperty<int> ElapsedSeconds => _elapsedSeconds;

    /// <summary>
    /// Gets the elapsed seconds as a display string.
    /// </summary>
    public ReadOnlyReactiveProperty<string> ElapsedDisplay => _elapsedDisplay;

    /// <summary>
    /// Gets a value indicating whether the timer is running.
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsRunning => _isRunning;

    /// <inheritdoc/>
    public void Dispose()
    {
        _viewModelDisposables.Dispose();
        _status.Dispose();
        _elapsedSeconds.Dispose();
        _elapsedDisplay.Dispose();
        _isRunning.Dispose();
    }
}
