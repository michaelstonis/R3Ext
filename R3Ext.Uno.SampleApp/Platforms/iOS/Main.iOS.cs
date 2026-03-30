// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using UIKit;

namespace R3Ext.Uno.SampleApp.iOS;

/// <summary>
/// Entry point for iOS platform.
/// </summary>
public class EntryPoint
{
    /// <summary>
    /// Main entry point for the iOS application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(App));
    }
}
