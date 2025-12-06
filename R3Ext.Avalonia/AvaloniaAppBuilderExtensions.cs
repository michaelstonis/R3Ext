// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using R3Ext.Activation;

namespace R3Ext.Avalonia;

/// <summary>
/// Extension methods for configuring R3Ext activation in Avalonia applications.
/// </summary>
public static class AvaloniaAppBuilderExtensions
{
    private static readonly object Lock = new();
    private static bool _isRegistered;

    /// <summary>
    /// Registers the Avalonia activation provider with R3Ext.
    /// Call this method during application startup to enable WhenActivated/WhenAttached
    /// for Avalonia visuals.
    /// </summary>
    /// <param name="builder">The Avalonia app builder.</param>
    /// <returns>The app builder for chaining.</returns>
    /// <example>
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;()
    ///     .UsePlatformDetect()
    ///     .UseR3Activation()
    ///     .StartWithClassicDesktopLifetime(args);
    /// </code>
    /// </example>
    public static AppBuilder UseR3Activation(this AppBuilder builder)
    {
        RegisterActivationProvider();
        return builder;
    }

    /// <summary>
    /// Registers the Avalonia activation provider with R3Ext.
    /// This method can be called independently if not using the AppBuilder pattern.
    /// </summary>
    public static void RegisterActivationProvider()
    {
        lock (Lock)
        {
            if (_isRegistered)
            {
                return;
            }

            ActivationProviderRegistry.Register(AvaloniaActivationProviders.GetActivation);
            _isRegistered = true;
        }
    }
}

/// <summary>
/// Extension methods for configuring R3Ext services with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds R3Ext Avalonia activation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddR3Activation(this IServiceCollection services)
    {
        AvaloniaAppBuilderExtensions.RegisterActivationProvider();
        return services;
    }
}
