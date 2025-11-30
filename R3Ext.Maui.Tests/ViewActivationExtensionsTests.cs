using Microsoft.Maui.Controls;
using R3;
using R3Ext.Activation;
using R3Ext.Maui;
using Xunit;

// Resolve ambiguity with Microsoft.Maui.ActivationState
using ActivationState = R3Ext.Activation.ActivationState;

namespace R3Ext.Maui.Tests;

/// <summary>
/// Tests for MAUI View activation extensions.
/// These tests verify the observable patterns work correctly with View types.
/// </summary>
public class ViewActivationExtensionsTests
{
    [Fact]
    public void GetActivation_ReturnsObservable()
    {
        // Arrange
        var view = new BoxView();

        // Act
        var activation = view.GetActivation();

        // Assert
        Assert.NotNull(activation);
    }

    [Fact]
    public void GetActivation_EmitsInitialState()
    {
        // Arrange
        var view = new BoxView { IsVisible = true };
        var states = new List<ActivationState>();

        // Act
        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));

        // Assert - Should emit initial state
        Assert.Single(states);
        Assert.Equal(ActivationState.Activated, states[0]);
    }

    [Fact]
    public void GetActivation_EmitsDeactivatedWhenNotVisible()
    {
        // Arrange
        var view = new BoxView { IsVisible = false };
        var states = new List<ActivationState>();

        // Act
        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));

        // Assert - Should emit initial state as Deactivated
        Assert.Single(states);
        Assert.Equal(ActivationState.Deactivated, states[0]);
    }

    [Fact]
    public void GetActivation_EmitsOnVisibilityChange()
    {
        // Arrange
        var view = new BoxView { IsVisible = true };
        var states = new List<ActivationState>();

        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));
        states.Clear(); // Clear initial state

        // Act
        view.IsVisible = false;

        // Assert
        Assert.Single(states);
        Assert.Equal(ActivationState.Deactivated, states[0]);
    }

    [Fact]
    public void GetActivation_EmitsActivatedWhenBecomingVisible()
    {
        // Arrange
        var view = new BoxView { IsVisible = false };
        var states = new List<ActivationState>();

        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));
        states.Clear(); // Clear initial state

        // Act
        view.IsVisible = true;

        // Assert
        Assert.Single(states);
        Assert.Equal(ActivationState.Activated, states[0]);
    }

    [Fact]
    public void GetActivation_DoesNotEmitWhenStateUnchanged()
    {
        // Arrange
        var view = new BoxView { IsVisible = true };
        var states = new List<ActivationState>();

        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));
        states.Clear(); // Clear initial state

        // Act - Setting to same value
        view.IsVisible = true;

        // Assert - Should not emit
        Assert.Empty(states);
    }

    [Fact]
    public void GetActivation_DisposingUnsubscribesFromEvents()
    {
        // Arrange
        var view = new BoxView { IsVisible = true };
        var states = new List<ActivationState>();

        var subscription = view.GetActivation().Subscribe(s => states.Add(s));
        states.Clear(); // Clear initial state

        // Act
        subscription.Dispose();
        view.IsVisible = false;

        // Assert - Should not receive any events after disposal
        Assert.Empty(states);
    }

    [Fact]
    public void GetLoadedActivation_ReturnsObservable()
    {
        // Arrange
        var view = new BoxView();

        // Act
        var activation = view.GetLoadedActivation();

        // Assert
        Assert.NotNull(activation);
    }

    [Fact]
    public void GetActivation_ThrowsOnNull()
    {
        // Arrange
        View? view = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => view!.GetActivation());
    }

    [Fact]
    public void GetLoadedActivation_ThrowsOnNull()
    {
        // Arrange
        View? view = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => view!.GetLoadedActivation());
    }

    [Fact]
    public void GetActivation_TracksMultipleVisibilityChanges()
    {
        // Arrange
        var view = new BoxView { IsVisible = true };
        var states = new List<ActivationState>();

        using var subscription = view.GetActivation().Subscribe(s => states.Add(s));

        // Act
        view.IsVisible = false;
        view.IsVisible = true;
        view.IsVisible = false;

        // Assert - Initial + 3 changes = 4 total
        Assert.Equal(4, states.Count);
        Assert.Equal(ActivationState.Activated, states[0]);   // Initial
        Assert.Equal(ActivationState.Deactivated, states[1]); // First change
        Assert.Equal(ActivationState.Activated, states[2]);   // Second change
        Assert.Equal(ActivationState.Deactivated, states[3]); // Third change
    }
}
