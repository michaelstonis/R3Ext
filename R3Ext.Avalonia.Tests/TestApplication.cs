// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace R3Ext.Avalonia.Tests;

/// <summary>
/// Test application for Avalonia headless testing.
/// </summary>
public class TestApplication : Application
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestApplication"/> class.
    /// </summary>
    public TestApplication()
    {
        Styles.Add(new FluentTheme());
    }

    /// <summary>
    /// Builds the Avalonia app for headless testing.
    /// </summary>
    /// <returns>The configured AppBuilder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        });
}
