using System;
using System.Collections.Generic;
using R3;

namespace R3Ext.Activation;

/// <summary>
/// Delegate that provides activation for a specific view type.
/// </summary>
/// <param name="view">The view to get activation for.</param>
/// <returns>The activation observable, or null if this provider doesn't handle the view type.</returns>
public delegate Observable<ActivationState>? ActivationProvider(object view);

/// <summary>
/// Extension methods for obtaining activation observables from views.
/// Platform libraries (R3Ext.Maui, R3Ext.Blazor, etc.) register their activation providers
/// using <see cref="ActivationProviderRegistry.Register"/>.
/// </summary>
public static class ViewActivation
{
    /// <summary>
    /// Gets the activation observable for a view.
    /// This method uses registered platform-specific activation providers.
    /// </summary>
    /// <param name="view">The activatable view.</param>
    /// <returns>An observable that emits activation state changes.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when no platform activation provider is registered for the view type.
    /// Ensure you have referenced a platform package (R3Ext.Maui, R3Ext.Blazor, etc.)
    /// and called its registration method.
    /// </exception>
    public static Observable<ActivationState> GetActivation(this IActivatableView view)
    {
        return ActivationProviderRegistry.GetActivation(view);
    }
}

/// <summary>
/// Registry for platform-specific activation providers.
/// Platform libraries register their providers during startup.
/// </summary>
public static class ActivationProviderRegistry
{
    private static readonly List<ActivationProvider> Providers = new();
    private static readonly object Lock = new();

    /// <summary>
    /// Registers an activation provider.
    /// Providers are tried in registration order until one returns a non-null result.
    /// </summary>
    /// <param name="provider">The activation provider to register.</param>
    public static void Register(ActivationProvider provider)
    {
        lock (Lock)
        {
            Providers.Add(provider);
        }
    }

    /// <summary>
    /// Gets activation for a view using registered providers.
    /// </summary>
    /// <param name="view">The view to get activation for.</param>
    /// <returns>The activation observable.</returns>
    /// <exception cref="NotSupportedException">Thrown when no provider handles the view.</exception>
    internal static Observable<ActivationState> GetActivation(IActivatableView view)
    {
        lock (Lock)
        {
            foreach (ActivationProvider provider in Providers)
            {
                Observable<ActivationState>? activation = provider(view);
                if (activation is not null)
                {
                    return activation;
                }
            }
        }

        throw new NotSupportedException(
            $"No activation provider registered for view type '{view.GetType().Name}'. " +
            "Ensure you have referenced a platform package (R3Ext.Maui, R3Ext.Blazor, etc.) " +
            "and called its registration method (e.g., UseR3Activation()).");
    }

    /// <summary>
    /// Clears all registered providers. Primarily for testing.
    /// </summary>
    internal static void Clear()
    {
        lock (Lock)
        {
            Providers.Clear();
        }
    }
}
