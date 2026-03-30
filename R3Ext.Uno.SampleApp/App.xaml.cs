// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using R3Ext.Uno.SampleApp.Views;

namespace R3Ext.Uno.SampleApp;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    protected Window? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Register the R3Ext Uno activation provider
        UnoHostBuilderExtensions.RegisterActivationProvider();

#if NET9_0_WINDOWS && !HAS_UNO
        // On native Windows with WinUI 3, create a new window
        MainWindow = new MainWindow();
#else
        // On Uno platforms (iOS, Android, MacCatalyst, etc.), use Window.Current
        MainWindow = Microsoft.UI.Xaml.Window.Current;

        // Ensure the window has content
        if (MainWindow?.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow!.Content = rootFrame;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(ActivationDemoPage));
        }
#endif

        MainWindow?.Activate();
    }
}
