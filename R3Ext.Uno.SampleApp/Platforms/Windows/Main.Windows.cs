// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

namespace R3Ext.Uno.SampleApp.Windows;

/// <summary>
/// Entry point for Windows platform.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point for the Windows application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    [global::System.STAThread]
    public static void Main(string[] args)
    {
        Microsoft.UI.Xaml.Application.Start(_ => new App());
    }
}
