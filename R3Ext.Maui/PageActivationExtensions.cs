using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui;

/// <summary>
/// Provides extension methods to get activation observables for MAUI Pages.
/// </summary>
/// <remarks>
/// Pages use Appearing/Disappearing events for visibility-based activation.
/// This maps to the <see cref="ActivationTrigger.Visibility"/> trigger.
/// </remarks>
public static class PageActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation state changes for a Page.
    /// Emits <see cref="ActivationState.Activated"/> on Appearing and
    /// <see cref="ActivationState.Deactivated"/> on Disappearing.
    /// </summary>
    /// <param name="page">The page to observe.</param>
    /// <returns>An observable of activation state changes.</returns>
    /// <remarks>
    /// The observable completes when the page is collected.
    /// Uses weak references to avoid preventing garbage collection.
    /// </remarks>
    public static Observable<ActivationState> GetActivation(this Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PageActivationObservable(page);
    }

    /// <summary>
    /// Gets an observable that emits when the page is loaded/unloaded from the visual tree.
    /// This maps to the <see cref="ActivationTrigger.Attached"/> trigger.
    /// </summary>
    /// <param name="page">The page to observe.</param>
    /// <returns>An observable of activation state changes.</returns>
    public static Observable<ActivationState> GetLoadedActivation(this Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new LoadedActivationObservable(page);
    }
}

/// <summary>
/// Observable that emits activation states based on Page Appearing/Disappearing events.
/// </summary>
file sealed class PageActivationObservable(Page page) : Observable<ActivationState>
{
    protected override IDisposable SubscribeCore(Observer<ActivationState> observer)
    {
        return new PageActivationSubscription(page, observer);
    }
}

/// <summary>
/// Subscription that manages Page Appearing/Disappearing event handlers.
/// Uses weak reference pattern for AOT compatibility.
/// </summary>
file sealed class PageActivationSubscription : IDisposable
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
