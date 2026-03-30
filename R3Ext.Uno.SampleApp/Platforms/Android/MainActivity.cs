// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Android.App;
using Android.Content.PM;
using Android.Views;

namespace R3Ext.Uno.SampleApp.Droid;

/// <summary>
/// Main activity for Android platform.
/// </summary>
[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
}
