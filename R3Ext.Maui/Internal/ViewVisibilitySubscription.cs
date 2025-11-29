using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Subscription that monitors IsVisible property changes.
/// </summary>
internal sealed class ViewVisibilitySubscription : IDisposable
{
    private readonly View _view;
    private readonly Observer<ActivationState> _observer;
    private bool _disposed;
    private bool _lastState;

    public ViewVisibilitySubscription(View view, Observer<ActivationState> observer)
    {
        _view = view;
        _observer = observer;
        _lastState = view.IsVisible;

        view.PropertyChanged += OnPropertyChanged;

        // Emit initial state
        observer.OnNext(_lastState ? ActivationState.Activated : ActivationState.Deactivated);
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(View.IsVisible))
        {
            return;
        }

        var currentState = _view.IsVisible;
        if (currentState != _lastState)
        {
            _lastState = currentState;
            _observer.OnNext(currentState ? ActivationState.Activated : ActivationState.Deactivated);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _view.PropertyChanged -= OnPropertyChanged;
    }
}
