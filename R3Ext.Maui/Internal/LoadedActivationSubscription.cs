using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Subscription that manages Loaded/Unloaded event handlers.
/// </summary>
internal sealed class LoadedActivationSubscription : IDisposable
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
