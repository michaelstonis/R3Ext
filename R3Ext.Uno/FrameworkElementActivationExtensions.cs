// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using R3;
using R3Ext.Activation;
using Windows.UI.Core;

namespace R3Ext.Uno;

/// <summary>
/// Extension methods for getting activation observables from Uno Platform FrameworkElements.
/// Uses the Loaded/Unloaded events as activation triggers.
/// </summary>
public static class FrameworkElementActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on Loaded/Unloaded events.
    /// Emits <see cref="ActivationState.Activated"/> when Loaded,
    /// <see cref="ActivationState.Deactivated"/> when Unloaded.
    /// </summary>
    /// <param name="element">The framework element to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetActivation(this FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return Observable.Create<ActivationState>(observer =>
        {
            // Emit initial state - check if already loaded
            if (element.IsLoaded)
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnLoaded(object sender, RoutedEventArgs e)
            {
                observer.OnNext(ActivationState.Activated);
            }

            void OnUnloaded(object sender, RoutedEventArgs e)
            {
                observer.OnNext(ActivationState.Deactivated);
            }

            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;

            return Disposable.Create(() =>
            {
                element.Loaded -= OnLoaded;
                element.Unloaded -= OnUnloaded;
            });
        });
    }

    /// <summary>
    /// Gets an observable that emits activation states based on Loaded/Unloaded events.
    /// This is the same as <see cref="GetActivation"/> for Uno Platform since Loaded/Unloaded
    /// is the natural attachment mechanism.
    /// </summary>
    /// <param name="element">The framework element to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetLoadedActivation(this FrameworkElement element)
    {
        // In Uno/WinUI, "loaded" is the Loaded event
        return element.GetActivation();
    }
}

/// <summary>
/// Extension methods for getting visibility-based activation observables from Uno Platform UIElements.
/// Uses the Visibility property for visibility-based activation.
/// </summary>
public static class UIElementActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on the element's Visibility property.
    /// Emits <see cref="ActivationState.Activated"/> when Visible,
    /// <see cref="ActivationState.Deactivated"/> when Collapsed.
    /// </summary>
    /// <param name="element">The UI element to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetVisibilityActivation(this UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return Observable.Create<ActivationState>(observer =>
        {
            // Emit initial state
            observer.OnNext(
                element.Visibility == Visibility.Visible
                    ? ActivationState.Activated
                    : ActivationState.Deactivated);

            // Register for Visibility changes via DependencyProperty callback
            long token = element.RegisterPropertyChangedCallback(
                UIElement.VisibilityProperty,
                (sender, dp) =>
                {
                    var uiElement = (UIElement)sender;
                    observer.OnNext(uiElement.Visibility == Visibility.Visible
                        ? ActivationState.Activated
                        : ActivationState.Deactivated);
                });

            return Disposable.Create(() =>
            {
                element.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, token);
            });
        });
    }
}

/// <summary>
/// Extension methods for getting activation observables from Uno Platform Windows.
/// Uses window lifecycle for activation tracking.
/// </summary>
public static class WindowActivationExtensions
{
    /// <summary>
    /// Gets an observable that emits activation states based on window activation state.
    /// Emits <see cref="ActivationState.Activated"/> when window becomes active,
    /// <see cref="ActivationState.Deactivated"/> when window is deactivated or closed.
    /// </summary>
    /// <param name="window">The window to observe.</param>
    /// <returns>An observable of activation states.</returns>
    public static Observable<ActivationState> GetActivation(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return Observable.Create<ActivationState>(observer =>
        {
            // Emit activated when subscribed (window exists)
            observer.OnNext(ActivationState.Activated);

            void OnActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
            {
                // WindowActivatedEventArgs.WindowActivationState is CoreWindowActivationState from Windows.UI.Core
                var state = e.WindowActivationState switch
                {
                    CoreWindowActivationState.CodeActivated => ActivationState.Activated,
                    CoreWindowActivationState.PointerActivated => ActivationState.Activated,
                    CoreWindowActivationState.Deactivated => ActivationState.Deactivated,
                    _ => ActivationState.Activated,
                };
                observer.OnNext(state);
            }

            void OnClosed(object sender, WindowEventArgs e)
            {
                observer.OnNext(ActivationState.Deactivated);
                observer.OnCompleted();
            }

            window.Activated += OnActivated;
            window.Closed += OnClosed;

            return Disposable.Create(() =>
            {
                window.Activated -= OnActivated;
                window.Closed -= OnClosed;
            });
        });
    }
}
