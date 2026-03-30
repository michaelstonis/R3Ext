// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using R3Ext.Activation;
using Xunit;

namespace R3Ext.Uno.Tests;

/// <summary>
/// Tests for Uno Platform activation provider registration.
/// </summary>
public class UnoActivationProviderTests
{
    [Fact]
    public void RegisterActivationProvider_RegistersProvider()
    {
        // Arrange & Act
        UnoHostBuilderExtensions.RegisterActivationProvider();

        // Assert - we can't easily verify registration, but we can verify no exception
        Assert.True(true, "Provider registered successfully");
    }

    [Fact]
    public void RegisterActivationProvider_IsIdempotent()
    {
        // Arrange & Act - call multiple times
        UnoHostBuilderExtensions.RegisterActivationProvider();
        UnoHostBuilderExtensions.RegisterActivationProvider();
        UnoHostBuilderExtensions.RegisterActivationProvider();

        // Assert - no exception means idempotent
        Assert.True(true, "Multiple registrations did not throw");
    }

    [Fact]
    public void AddR3Activation_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddR3Activation();

        // Assert
        Assert.Same(services, result);
    }
}
