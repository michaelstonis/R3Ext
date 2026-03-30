// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.SampleApp.ViewModels;

namespace R3Ext.Uno.SampleApp.Views;

/// <summary>
/// Demonstrates the WhenAttached pattern for long-lived subscriptions.
/// This page shows how timers and other resources persist across navigation.
/// </summary>
public sealed partial class TimerDemoPage : Page, IViewFor<TimerDemoViewModel>
{
    public TimerDemoPage()
    {
        this.InitializeComponent();

        // Create and set the ViewModel from the shared library
        ViewModel = new TimerDemoViewModel();
        DataContext = ViewModel;

        // Use WhenAttached for subscriptions that should persist while the page is loaded
        this.WhenAttached((ref DisposableBag disposables) =>
        {
            // Subscribe to timer display
            ViewModel!.TimerDisplay
                .Subscribe(display => TimerLabel.Text = display)
                .AddTo(ref disposables);

            ViewModel.StatusMessage
                .Subscribe(status => StatusLabel.Text = status)
                .AddTo(ref disposables);
        });
    }

    /// <summary>
    /// Gets or sets the ViewModel for this view.
    /// </summary>
    public TimerDemoViewModel? ViewModel { get; set; }

    /// <summary>
    /// Gets a value indicating whether to auto-activate the ViewModel.
    /// </summary>
    public bool AutoActivateViewModel => true;

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.StartCommand.Execute(default);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.StopCommand.Execute(default);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetCommand.Execute(default);
    }
}
