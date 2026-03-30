// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.Uno.SampleApp.Views;

/// <summary>
/// Demonstrates the <see cref="IViewFor{TViewModel}"/> pattern with automatic ViewModel activation.
/// This page shows how to use R3Ext activation with Uno Platform.
/// </summary>
public sealed partial class ActivationDemoPage : Page, IViewFor<ActivationDemoViewModel>
{
    public ActivationDemoPage()
    {
        this.InitializeComponent();

        // Create and set the ViewModel from the shared library
        ViewModel = new ActivationDemoViewModel();
        DataContext = ViewModel;

        // Set up view-side WhenActivated
        this.WhenActivated((ref DisposableBag disposables) =>
        {
            // Subscribe to ViewModel properties and update UI
            ViewModel!.ActivationStatus
                .Subscribe(status => StatusLabel.Text = status)
                .AddTo(ref disposables);

            ViewModel.TimerDisplay
                .Subscribe(timer => TimerLabel.Text = timer)
                .AddTo(ref disposables);

            ViewModel.IsTimerRunning
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
