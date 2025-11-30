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
    /// Supports late-bound ViewModels - the activator is fetched on each activation.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The MAUI view implementing IViewFor.</param>
    /// <param name="block">The block to execute on activation.</param>
    /// <remarks>
    /// The subscription is automatically disposed when the element's Window becomes null
    /// (i.e., when the element is removed from the visual tree). You do not need to manage
    /// the returned disposable unless you want to stop monitoring before the element is removed.
    /// </remarks>
    /// <returns>An IDisposable that stops monitoring when disposed.</returns>
    public static IDisposable WhenActivated<TViewModel>(
        this IViewFor<TViewModel> view,
        ActivationBlock block)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        Observable<ActivationState> activation = view.GetActivation();

        // Create a delegate that fetches the activator each time (supports late-bound ViewModels)
        Func<ViewModelActivator?>? getActivator = view.AutoActivateViewModel
            ? () => (view.ViewModel as IActivatableViewModel)?.Activator
            : null;

        var state = new MauiActivationState(block, getActivator);

        IDisposable subscription = activation.Subscribe(new MauiActivationObserver(state));

        IDisposable combined = Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmActivationHandle?.Dispose();
            }));

        // Auto-dispose when the element is removed from the window
        return AttachToWindowLifecycle(view, combined);
    }

    /// <summary>
    /// Executes a block when the MAUI page/view is attached to the visual tree.
    /// Uses Loaded/Unloaded events.
    /// Automatically attaches the associated view model if <see cref="IViewFor{TViewModel}.AutoActivateViewModel"/> is true
    /// and the ViewModel implements <see cref="IAttachableViewModel"/>.
    /// Supports late-bound ViewModels - the attacher is fetched on each attachment.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type.</typeparam>
    /// <param name="view">The MAUI view implementing IViewFor.</param>
    /// <param name="block">The block to execute on attachment.</param>
    /// <remarks>
    /// The subscription is automatically disposed when the element is unloaded.
    /// You do not need to manage the returned disposable unless you want to stop
    /// monitoring before the element is unloaded.
    /// </remarks>
    /// <returns>An IDisposable that stops monitoring when disposed.</returns>
    public static IDisposable WhenAttached<TViewModel>(
        this IViewFor<TViewModel> view,
        ActivationBlock block)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(block);

        Observable<ActivationState> activation = view.GetLoadedActivation();

        // Create a delegate that fetches the attacher each time (supports late-bound ViewModels)
        Func<ViewModelAttacher?>? getAttacher = view.AutoActivateViewModel
            ? () => (view.ViewModel as IAttachableViewModel)?.Attacher
            : null;

        var state = new MauiAttachmentState(block, getAttacher);

        IDisposable subscription = activation.Subscribe(new MauiAttachmentObserver(state));

        IDisposable combined = Disposable.Combine(
            subscription,
            Disposable.Create(state, static s =>
            {
                s.CurrentBag.Dispose();
                s.VmAttachmentHandle?.Dispose();
            }));

        // Auto-dispose when the element is unloaded
        return AttachToLoadedLifecycle(view, combined);
    }

    /// <summary>
    /// Attaches a disposable to an element's window lifecycle so it's automatically disposed
    /// when the element's Window becomes null (removed from visual tree).
    /// </summary>
    private static IDisposable AttachToWindowLifecycle<TViewModel>(IViewFor<TViewModel> view, IDisposable disposable)
        where TViewModel : class
    {
        if (view is not VisualElement element)
        {
            // If not a VisualElement, just return the disposable as-is
            return disposable;
        }

        var disposed = false;

        void OnWindowChanged(object? sender, EventArgs e)
        {
            // When the window becomes null, the element is being removed from the visual tree
            if (element.Window is null && !disposed)
            {
                disposed = true;
                element.PropertyChanged -= OnPropertyChanged;
                disposable.Dispose();
            }
        }

        void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualElement.Window))
            {
                OnWindowChanged(sender, e);
            }
        }

        element.PropertyChanged += OnPropertyChanged;

        // Return a wrapper that also removes the event handler when manually disposed
        return Disposable.Create(() =>
        {
            if (!disposed)
            {
                disposed = true;
                element.PropertyChanged -= OnPropertyChanged;
                disposable.Dispose();
            }
        });
    }

    /// <summary>
    /// Attaches a disposable to an element's loaded/unloaded lifecycle so it's automatically disposed
    /// when the element is unloaded.
    /// </summary>
    private static IDisposable AttachToLoadedLifecycle<TViewModel>(IViewFor<TViewModel> view, IDisposable disposable)
        where TViewModel : class
    {
        if (view is not VisualElement element)
        {
            // If not a VisualElement, just return the disposable as-is
            return disposable;
        }

        var disposed = false;

        void OnUnloaded(object? sender, EventArgs e)
        {
            if (!disposed)
            {
                disposed = true;
                element.Unloaded -= OnUnloaded;
                disposable.Dispose();
            }
        }

        element.Unloaded += OnUnloaded;

        // Return a wrapper that also removes the event handler when manually disposed
        return Disposable.Create(() =>
        {
            if (!disposed)
            {
                disposed = true;
                element.Unloaded -= OnUnloaded;
                disposable.Dispose();
            }
        });
    }
}
