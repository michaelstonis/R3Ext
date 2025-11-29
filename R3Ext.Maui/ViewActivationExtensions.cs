using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui;

/// <summary>
/// Provides extension methods to get activation observables for MAUI Views.
/// </summary>
/// <remarks>
/// Views use IsVisible property changes for visibility-based activation.
/// This maps to the <see cref="ActivationTrigger.Visibility"/> trigger.
/// </remarks>
public static class ViewActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation state changes for a View based on visibility.
    /// Emits <see cref="ActivationState.Activated"/> when IsVisible becomes true and
    /// <see cref="ActivationState.Deactivated"/> when IsVisible becomes false.
    /// </summary>
    /// <param name="view">The view to observe.</param>
    /// <returns>An observable of activation state changes.</returns>
    public static Observable<ActivationState> GetActivation(this View view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ViewVisibilityActivationObservable(view);
    }

    /// <summary>
    /// Gets an observable that emits when the view is loaded/unloaded from the visual tree.
    /// This maps to the <see cref="ActivationTrigger.Attached"/> trigger.
    /// </summary>
    /// <param name="view">The view to observe.</param>
    /// <returns>An observable of activation state changes.</returns>
    public static Observable<ActivationState> GetLoadedActivation(this View view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new LoadedActivationObservable(view);
    }
}

/// <summary>
/// Observable that emits activation states based on View IsVisible property changes.
/// </summary>
file sealed class ViewVisibilityActivationObservable(View view) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new ViewVisibilitySubscription(view, observer);
    }
}

/// <summary>
/// Subscription that monitors IsVisible property changes.
/// </summary>
file sealed class ViewVisibilitySubscription : IDisposable
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

/// <summary>
/// Observable that emits activation states based on Loaded/Unloaded events.
/// </summary>
file sealed class LoadedActivationObservable(VisualElement element) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new LoadedActivationSubscription(element, observer);
    }
}

/// <summary>
/// Subscription that manages Loaded/Unloaded event handlers.
/// </summary>
file sealed class LoadedActivationSubscription : IDisposable
{
    private readonly VisualElement _element;
    private readonly Observer<ActivationState> _observer;
    private bool _disposed;

    public LoadedActivationSubscription(VisualElement element, Observer<ActivationState> observer)
    {
        _element = element;
        _observer = observer;

        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;

        // Emit initial state if element is already loaded
        if (element.IsLoaded)
        {
            observer.OnNext(ActivationState.Activated);
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _observer.OnNext(ActivationState.Activated);
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _observer.OnNext(ActivationState.Deactivated);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _element.Loaded -= OnLoaded;
        _element.Unloaded -= OnUnloaded;
    }
}
