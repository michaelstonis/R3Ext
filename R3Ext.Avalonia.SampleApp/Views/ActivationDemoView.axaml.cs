// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Avalonia;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.Avalonia.SampleApp.Views;

/// <summary>
/// Demonstrates the <see cref="IViewFor{TViewModel}"/> pattern with automatic ViewModel activation.
/// This view shows how to use R3Ext bindings with Avalonia.
/// </summary>
public partial class ActivationDemoView : UserControl, IViewFor<ActivationDemoViewModel>
{
    public ActivationDemoView()
    {
        InitializeComponent();

        // Create and set the ViewModel from the shared library
        ViewModel = new ActivationDemoViewModel();
        DataContext = ViewModel;

        // Set up view-side WhenActivated with R3Ext bindings
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            // R3Ext bindings - bind ViewModel's ReactiveProperty to UI controls
            // The source generator generates the binding implementation
            this.BindOneWay(StatusLabel, v => v.ViewModel!.ActivationStatus.CurrentValue, l => l.Text)
                .AddTo(ref disposables);

            this.BindOneWay(TimerLabel, v => v.ViewModel!.TimerDisplay.CurrentValue, l => l.Text)
                .AddTo(ref disposables);

            // Use direct observable subscription for IsTimerRunning
            ViewModel!.IsTimerRunning
                .Subscribe(running => TimerStatusLabel.Text = running ? "Timer is running..." : "Timer stopped")
                .AddTo(ref disposables);

            ViewModel.ActivationCount
                .Subscribe(count => ActivationCountLabel.Text = count.ToString())
                .AddTo(ref disposables);

            ViewModel.DeactivationCount
                .Subscribe(count => DeactivationCountLabel.Text = count.ToString())
                .AddTo(ref disposables);

            ViewModel.AttachmentCount
                .Subscribe(count => AttachmentCountLabel.Text = count.ToString())
                .AddTo(ref disposables);

            ViewModel.DetachmentCount
                .Subscribe(count => DetachmentCountLabel.Text = count.ToString())
                .AddTo(ref disposables);
        });
    }

    /// <summary>
    /// Gets or sets the ViewModel for this view.
    /// </summary>
    public ActivationDemoViewModel? ViewModel { get; set; }

    /// <summary>
    /// Gets a value indicating whether to auto-activate the ViewModel.
    /// </summary>
    public bool AutoActivateViewModel => true;
}
