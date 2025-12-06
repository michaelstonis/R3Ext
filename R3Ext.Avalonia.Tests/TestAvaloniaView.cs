// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using R3;
using R3Ext.Activation;

namespace R3Ext.Avalonia.Tests;

/// <summary>
/// Test view that implements IActivatableView.
/// </summary>
public class TestAvaloniaView : UserControl, IActivatableView
{
    /// <inheritdoc />
    public Observable<ActivationState> Activation =>
        this.GetActivation();
}

/// <summary>
/// Test view that implements IViewFor with a ViewModel.
/// </summary>
/// <typeparam name="TViewModel">The view model type.</typeparam>
public class TestAvaloniaViewFor<TViewModel> : UserControl, IViewFor<TViewModel>
    where TViewModel : class
{
    /// <summary>
    /// Gets or sets the ViewModel.
    /// </summary>
    public TViewModel? ViewModel { get; set; }

    /// <inheritdoc />
    public Observable<ActivationState> Activation =>
        this.GetActivation();
}

/// <summary>
/// Test ViewModel that implements IActivatableViewModel.
/// </summary>
public class TestActivatableViewModel : IActivatableViewModel
{
    /// <summary>
    /// Gets the activator for this ViewModel.
    /// </summary>
    public ViewModelActivator Activator { get; } = new ViewModelActivator();
}

/// <summary>
/// Test ViewModel that implements IAttachableViewModel.
/// </summary>
public class TestAttachableViewModel : IAttachableViewModel
{
    /// <summary>
    /// Gets the attacher for this ViewModel.
    /// </summary>
    public ViewModelAttacher Attacher { get; } = new ViewModelAttacher();
}
