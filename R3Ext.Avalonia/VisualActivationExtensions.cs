// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using R3;
using R3Ext.Activation;

namespace R3Ext.Avalonia;

/// <summary>
/// Extension methods for getting activation observables from Avalonia controls.
/// Uses the Visual tree attachment/detachment as activation triggers.
/// </summary>
public static class VisualActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on visual tree attachment.
    /// Emits <see cref="ActivationState.Activated"/> when attached to visual tree,
    /// <see cref="ActivationState.Deactivated"/> when detached.
    /// </summary>
    /// <param name="visual">The visual to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetActivation(this Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        return Observable.Create<ActivationState>(observer =>
        {
            // Emit initial state
            if (visual.IsAttachedToVisualTree())
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                observer.OnNext(ActivationState.Deactivated);
            }

            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;

            return Disposable.Create(() =>
            {
                visual.AttachedToVisualTree -= OnAttached;
                visual.DetachedFromVisualTree -= OnDetached;
            });
        });
    }

    /// <summary>
    /// Gets an observable that emits activation states based on visual tree attachment.
    /// This is the same as <see cref="GetActivation"/> for Avalonia since visual tree
    /// attachment is the natural attachment mechanism.
    /// </summary>
    /// <param name="visual">The visual to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetLoadedActivation(this Visual visual)
    {
        // In Avalonia, "loaded" is equivalent to visual tree attachment
        return visual.GetActivation();
    }
}

/// <summary>
/// Extension methods for getting activation observables from Avalonia Controls.
/// Uses IsVisible property for visibility-based activation.
/// </summary>
public static class ControlActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on the control's visibility.
    /// Emits <see cref="ActivationState.Activated"/> when visible,
    /// <see cref="ActivationState.Deactivated"/> when not visible.
    /// </summary>
    /// <param name="control">The control to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetVisibilityActivation(this Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return Observable.Create<ActivationState>(observer =>
        {
            // Emit initial state
            observer.OnNext(control.IsVisible ? ActivationState.Activated : ActivationState.Deactivated);

            void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    var isVisible = (bool)e.NewValue!;
                    observer.OnNext(isVisible ? ActivationState.Activated : ActivationState.Deactivated);
                }
            }

            control.PropertyChanged += OnPropertyChanged;

            return Disposable.Create(() =>
            {
                control.PropertyChanged -= OnPropertyChanged;
            });
        });
    }
}

/// <summary>
/// Extension methods for getting activation observables from Avalonia Windows.
/// Uses window lifecycle for activation tracking.
/// </summary>
public static class WindowActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on window opening/closing.
    /// Emits <see cref="ActivationState.Activated"/> when opened,
    /// <see cref="ActivationState.Deactivated"/> when closed.
    /// </summary>
    /// <param name="window">The window to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetActivation(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return Observable.Create<ActivationState>(observer =>
        {
            // If window is already visible/open, emit activated
            if (window.IsVisible)
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnOpened(object? sender, EventArgs e)
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnClosed(object? sender, EventArgs e)
            {
                observer.OnNext(ActivationState.Deactivated);
                observer.OnCompleted();
            }

            window.Opened += OnOpened;
            window.Closed += OnClosed;

            return Disposable.Create(() =>
            {
                window.Opened -= OnOpened;
                window.Closed -= OnClosed;
            });
        });
    }
}
