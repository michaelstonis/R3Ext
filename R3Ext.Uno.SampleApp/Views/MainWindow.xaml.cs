// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace R3Ext.Uno.SampleApp.Views;

/// <summary>
/// Main window with navigation to demo pages.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Navigate to activation demo by default
        ContentFrame.Navigate(typeof(ActivationDemoPage));
    }

    private void ActivationDemoButton_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ActivationDemoPage));
    }

    private void TimerDemoButton_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(TimerDemoPage));
    }
}
