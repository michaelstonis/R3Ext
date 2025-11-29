using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace R3Ext.Maui;

/// <summary>
/// Extension methods for configuring R3Ext activation support in MAUI applications.
/// </summary>
public static class MauiAppBuilderExtensions
{
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

        // Register activation services
        // Currently this is a marker method for future services
        // The actual activation is done via extension methods on Page/View

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

        var options = new R3ActivationOptions();
        configure(options);

        // Apply options to services if needed
        builder.Services.AddSingleton(options);

        return builder;
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
