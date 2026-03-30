// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Android.App;
using Android.Runtime;

namespace R3Ext.Uno.SampleApp.Droid;

/// <summary>
/// Main application class for Android platform.
/// </summary>
[global::Android.App.ApplicationAttribute(
    Label = "@string/ApplicationName",
    Icon = "@mipmap/icon",
    LargeHeap = true,
    HardwareAccelerated = true,
    Theme = "@style/AppTheme")]
public class Application : Microsoft.UI.Xaml.NativeApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Application"/> class.
    /// </summary>
    /// <param name="javaReference">Java reference handle.</param>
    /// <param name="transfer">JNI handle ownership transfer mode.</param>
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(() => new App(), javaReference, transfer)
    {
    }
}
