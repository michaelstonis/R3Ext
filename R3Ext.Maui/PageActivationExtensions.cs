using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Maui.Internal;

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
