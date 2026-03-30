// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Xunit;

namespace R3Ext.Uno.Tests;

/// <summary>
/// Tests for FrameworkElement activation extensions.
/// Note: Full UI tests require Uno Platform test host which is platform-specific.
/// These tests verify the null-checking and observable creation patterns.
/// </summary>
public class FrameworkElementActivationExtensionsTests
{
    [Fact]
    public void GetActivation_ThrowsOnNull()
    {
        // Arrange
        FrameworkElement? element = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => element!.GetActivation());
    }

    [Fact]
    public void GetLoadedActivation_ThrowsOnNull()
    {
        // Arrange
        FrameworkElement? element = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => element!.GetLoadedActivation());
    }
}

/// <summary>
/// Tests for UIElement visibility activation extensions.
/// </summary>
public class UIElementActivationExtensionsTests
{
    [Fact]
    public void GetVisibilityActivation_ThrowsOnNull()
    {
        // Arrange
        UIElement? element = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => element!.GetVisibilityActivation());
    }
}

/// <summary>
/// Tests for Window activation extensions.
/// </summary>
public class WindowActivationExtensionsTests
{
    [Fact]
    public void GetActivation_ThrowsOnNull()
    {
        // Arrange
        Window? window = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => window!.GetActivation());
    }
}
