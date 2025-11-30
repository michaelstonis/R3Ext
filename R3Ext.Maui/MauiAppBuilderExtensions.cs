using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using R3;
using R3Ext.Activation;

namespace R3Ext.Maui;

/// <summary>
/// Extension methods for configuring R3Ext activation support in MAUI applications.
/// </summary>
public static class MauiAppBuilderExtensions
{
    private static int _registered;

    /// <summary>
    /// Configures R3Ext activation support for the MAUI application.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the R3Ext MAUI activation services with the application's
    /// dependency injection container. It enables the use of <c>WhenActivated</c> and
    /// <c>WhenAttached</c> extension methods on Pages and Views that implement
    /// <see cref="R3Ext.Activation.IViewFor{TViewModel}"/>.
    /// </para>
    /// <para>
    /// Call this method in your <c>MauiProgram.cs</c>:
    /// </para>
    /// <code>
    /// public static MauiApp CreateMauiApp()
    /// {
    ///     var builder = MauiApp.CreateBuilder();
    ///     builder
    ///         .UseMauiApp&lt;App&gt;()
    ///         .UseR3()           // R3 core
    ///         .UseR3Activation() // R3Ext activation support
    ///         .ConfigureFonts(fonts => { ... });
    ///
    ///     return builder.Build();
    /// }
    /// </code>
    /// </remarks>
    public static MauiAppBuilder UseR3Activation(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the MAUI activation provider with the platform-agnostic registry.
        // This is idempotent - calling UseR3Activation multiple times is safe.
        RegisterMauiActivationProvider();

        return builder;
    }

    /// <summary>
    /// Configures R3Ext activation support with custom options.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <param name="configure">An action to configure activation options.</param>
    /// <returns>The builder for chaining.</returns>
    public static MauiAppBuilder UseR3Activation(
        this MauiAppBuilder builder,
        Action<R3ActivationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        RegisterMauiActivationProvider();

        var options = new R3ActivationOptions();
        configure(options);

        // Apply options to services if needed
        builder.Services.AddSingleton(options);

        return builder;
    }

    /// <summary>
    /// Registers the MAUI-specific activation provider with the platform-agnostic registry.
    /// This method is idempotent.
    /// </summary>
    private static void RegisterMauiActivationProvider()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // Register the MAUI activation provider.
        // It handles Page and View types by delegating to the appropriate extension methods.
        ActivationProviderRegistry.Register(MauiActivationProvider);
    }

    private static Observable<ActivationState>? MauiActivationProvider(object view)
    {
        return view switch
        {
            Page page => page.GetActivation(),
            View mauiView => mauiView.GetActivation(),
            _ => null,
        };
    }
}

/// <summary>
/// Options for configuring R3Ext activation behavior.
/// </summary>
public sealed class R3ActivationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to automatically activate view models when views are activated.
    /// Default is <c>true</c>.
    /// </summary>
    public bool AutoActivateViewModels { get; set; } = true;

    /// <summary>
    /// Gets or sets the default activation trigger for pages.
    /// Default is <see cref="R3Ext.Activation.ActivationTrigger.Visibility"/>.
    /// </summary>
    public Activation.ActivationTrigger DefaultPageTrigger { get; set; } =
        Activation.ActivationTrigger.Visibility;

    /// <summary>
    /// Gets or sets the default activation trigger for views.
    /// Default is <see cref="R3Ext.Activation.ActivationTrigger.Visibility"/>.
    /// </summary>
    public Activation.ActivationTrigger DefaultViewTrigger { get; set; } =
        Activation.ActivationTrigger.Visibility;
}
