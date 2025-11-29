using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Maui.Internal;

namespace R3Ext.Maui;

/// <summary>
/// MAUI-specific extensions for views implementing <see cref="IViewFor{TViewModel}"/>.
/// These extensions provide the glue between MAUI lifecycle events and the core activation system.
/// </summary>
public static class MauiActivatableViewExtensions
{
    /// <summary>
    /// Gets the activation observable for a MAUI Page that implements IViewFor.
    /// Uses Appearing/Disappearing events (Visibility trigger).
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="page">The page implementing IViewFor.</param>
    /// <returns>An observable of activation state changes.</returns>
    public static Observable<ActivationState> GetActivation<TViewModel>(
        this IViewFor<TViewModel> page)
        where TViewModel : class
    {
        return page switch
        {
            Page p => p.GetActivation(),
            View v => v.GetActivation(),
            _ => throw new NotSupportedException(
                $"Type {page.GetType().Name} is not a supported MAUI view type. " +
                "Expected Page or View."),
        };
    }

    /// <summary>
    /// Gets the loaded/unloaded activation observable for a MAUI element that implements IViewFor.
    /// Uses Loaded/Unloaded events (Attached trigger).
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="element">The element implementing IViewFor.</param>
    /// <returns>An observable of activation state changes.</returns>
    public static Observable<ActivationState> GetLoadedActivation<TViewModel>(
        this IViewFor<TViewModel> element)
        where TViewModel : class
    {
        return element switch
        {
            Page p => p.GetLoadedActivation(),
            View v => v.GetLoadedActivation(),
            _ => throw new NotSupportedException(
                $"Type {element.GetType().Name} is not a supported MAUI view type. " +
                "Expected Page or View."),
        };
    }

    /// <summary>
    /// Executes a block when the MAUI page/view is activated (visibility-based).
    /// Automatically activates the associated view model if <see cref="IViewFor{TViewModel}.AutoActivateViewModel"/> is true.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The MAUI view implementing IViewFor.</param>
    /// <param name="block">The block to execute on activation.</param>
    /// <returns>An IDisposable that stops monitoring when disposed.</returns>
    public static IDisposable WhenActivated<TViewModel>(
        this IViewFor<TViewModel> view,
        ActivationBlock block)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        var activation = view.GetActivation();
        var vmActivator = GetViewModelActivator(view);
        var state = new MauiActivationState(block, vmActivator);

        var subscription = activation.Subscribe(new MauiActivationObserver(state));

        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));
    }

    /// <summary>
    /// Executes a block when the MAUI page/view is attached to the visual tree.
    /// Uses Loaded/Unloaded events.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The MAUI view implementing IViewFor.</param>
    /// <param name="block">The block to execute on attachment.</param>
    /// <returns>An IDisposable that stops monitoring when disposed.</returns>
    public static IDisposable WhenAttached<TViewModel>(
        this IViewFor<TViewModel> view,
        ActivationBlock block)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        var activation = view.GetLoadedActivation();
        var state = new MauiActivationState(block, null);

        var subscription = activation.Subscribe(new MauiActivationObserver(state));

        return Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));
    }

    private static ViewModelActivator? GetViewModelActivator<TViewModel>(IViewFor<TViewModel> view)
        where TViewModel : class
    {
        if (!view.AutoActivateViewModel)
        {
            return null;
        }

        return (view.ViewModel as IActivatableViewModel)?.Activator;
    }
}
