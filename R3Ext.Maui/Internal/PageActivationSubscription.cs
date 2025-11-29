using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui.Internal;

/// <summary>
/// Subscription that manages Page Appearing/Disappearing event handlers.
/// </summary>
internal sealed class PageActivationSubscription : IDisposable
{
    private readonly Page _page;
    private readonly Observer<ActivationState> _observer;
    private bool _disposed;

    public PageActivationSubscription(Page page, Observer<ActivationState> observer)
    {
        _page = page;
        _observer = observer;

        page.Appearing += OnAppearing;
        page.Disappearing += OnDisappearing;

        // Emit initial state if page is already visible
        // Note: There's no direct "IsAppearing" property, so we rely on events
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _observer.OnNext(ActivationState.Activated);
        }
    }

    private void OnDisappearing(object? sender, EventArgs e)
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
        _page.Appearing -= OnAppearing;
        _page.Disappearing -= OnDisappearing;
    }
}
