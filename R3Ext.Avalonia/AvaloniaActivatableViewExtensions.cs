// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Avalonia.Internal;

namespace R3Ext.Avalonia;

/// <summary>
/// Avalonia-specific activation extension methods for Visuals that implement IViewFor.
/// </summary>
public static class AvaloniaActivatableViewExtensions
{
    /// <summary>
    /// Executes an activation block when the visual is attached to the visual tree.
    /// Automatically cleans up when detached.
    /// If the view implements <see cref="IViewFor{TViewModel}"/> and the ViewModel implements
    /// <see cref="IActivatableViewModel"/>, the ViewModel is also activated/deactivated.
    /// </summary>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <param name="view">The view to observe.</param>
    /// <param name="activationBlock">The block to execute on activation.</param>
    /// <returns>A disposable that, when disposed, stops observing activation.</returns>
    public static IDisposable WhenActivated<TView>(
        this TView view,
        ActivationBlock activationBlock)
        where TView : Visual, IActivatableView
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(activationBlock);

        // Try to get ViewModel activator if the view implements IViewFor<T>
        Func<ViewModelActivator?>? getActivator = TryGetViewModelActivatorFunc(view);

        var state = new AvaloniaActivationState(view, activationBlock, getActivator);
        return state.Start();
    }

    /// <summary>
    /// Executes an activation block when the visual is attached to the visual tree.
    /// Automatically cleans up when detached.
    /// Also auto-activates the ViewModel if it implements <see cref="IActivatableViewModel"/>.
    /// </summary>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <typeparam name="TViewModel">The type of the view model.</typeparam>
    /// <param name="view">The view to observe.</param>
    /// <param name="activationBlock">The block to execute on activation.</param>
    /// <returns>A disposable that, when disposed, stops observing activation.</returns>
    public static IDisposable WhenActivated<TView, TViewModel>(
        this TView view,
        ActivationBlock activationBlock)
        where TView : Visual, IViewFor<TViewModel>
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(activationBlock);

        Func<ViewModelActivator?>? getActivator = null;
        if (typeof(IActivatableViewModel).IsAssignableFrom(typeof(TViewModel)))
        {
            getActivator = () => (view.ViewModel as IActivatableViewModel)?.Activator;
        }

        var state = new AvaloniaActivationState(view, activationBlock, getActivator);
        return state.Start();
    }

    /// <summary>
    /// Executes an activation block when the visual is attached to the visual tree.
    /// This is the same as WhenActivated for Avalonia since visual tree attachment
    /// is the natural attachment mechanism.
    /// </summary>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <param name="view">The view to observe.</param>
    /// <param name="activationBlock">The block to execute on attachment.</param>
    /// <returns>A disposable that, when disposed, stops observing attachment.</returns>
    public static IDisposable WhenAttached<TView>(
        this TView view,
        ActivationBlock activationBlock)
        where TView : Visual, IActivatableView
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(activationBlock);

        // In Avalonia, WhenAttached uses the same visual tree events as WhenActivated
        var state = new AvaloniaActivationState(view, activationBlock);
        return state.Start();
    }

    /// <summary>
    /// Executes an activation block when the visual is attached to the visual tree.
    /// Also auto-attaches the ViewModel if it implements <see cref="IAttachableViewModel"/>.
    /// </summary>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <typeparam name="TViewModel">The type of the view model.</typeparam>
    /// <param name="view">The view to observe.</param>
    /// <param name="activationBlock">The block to execute on attachment.</param>
    /// <returns>A disposable that, when disposed, stops observing attachment.</returns>
    public static IDisposable WhenAttached<TView, TViewModel>(
        this TView view,
        ActivationBlock activationBlock)
        where TView : Visual, IViewFor<TViewModel>
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(activationBlock);

        Func<ViewModelAttacher?>? getAttacher = null;
        if (typeof(IAttachableViewModel).IsAssignableFrom(typeof(TViewModel)))
        {
            getAttacher = () => (view.ViewModel as IAttachableViewModel)?.Attacher;
        }

        var state = new AvaloniaAttachmentState(view, activationBlock, getAttacher);
        return state.Start();
    }

    /// <summary>
    /// Tries to get a function that returns the ViewModel's activator if the view implements IViewFor
    /// and the ViewModel implements IActivatableViewModel.
    /// </summary>
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2075:UnrecognizedReflectionPattern",
        Justification = "IViewFor<T> interface and ViewModel property are preserved through direct usage.")]
    private static Func<ViewModelActivator?>? TryGetViewModelActivatorFunc(object view)
    {
        // Find IViewFor<T> interface on the view
        var viewType = view.GetType();
        var viewForInterface = viewType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IViewFor<>));

        if (viewForInterface is null)
        {
            return null;
        }

        // Get the ViewModel type
        var viewModelType = viewForInterface.GetGenericArguments()[0];

        // Check if ViewModel type implements IActivatableViewModel
        if (!typeof(IActivatableViewModel).IsAssignableFrom(viewModelType))
        {
            return null;
        }

        // Get the ViewModel property
        var viewModelProperty = viewForInterface.GetProperty(nameof(IViewFor<object>.ViewModel));
        if (viewModelProperty is null)
        {
            return null;
        }

        // Return a function that gets the activator from the ViewModel
        return () =>
        {
            var viewModel = viewModelProperty.GetValue(view);
            return (viewModel as IActivatableViewModel)?.Activator;
        };
    }
}
