using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Maui.Internal;

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
