using R3;
using R3Ext.Activation;
using Xunit;

namespace R3Ext.Tests.Activation;

public class ViewModelActivatorTests
{
    [Fact]
    public void Activate_EmitsActivatedState()
    {
        // Arrange
        var activator = new ViewModelActivator();
        var states = new List<ActivationState>();
        using var sub = activator.Activation.Subscribe(s => states.Add(s));

        // Act
        using var handle = activator.Activate();

        // Assert
        Assert.Single(states);
        Assert.Equal(ActivationState.Activated, states[0]);
        Assert.True(activator.IsActivated);
    }

    [Fact]
    public void Deactivate_EmitsDeactivatedState()
    {
        // Arrange
        var activator = new ViewModelActivator();
        var states = new List<ActivationState>();
        using var sub = activator.Activation.Subscribe(s => states.Add(s));

        // Act
        var handle = activator.Activate();
        handle.Dispose();

        // Assert
        Assert.Equal(2, states.Count);
        Assert.Equal(ActivationState.Activated, states[0]);
        Assert.Equal(ActivationState.Deactivated, states[1]);
        Assert.False(activator.IsActivated);
    }

    [Fact]
    public void MultipleActivations_AreReferenceCounted()
    {
        // Arrange
        var activator = new ViewModelActivator();
        var states = new List<ActivationState>();
        using var sub = activator.Activation.Subscribe(s => states.Add(s));

        // Act
        var handle1 = activator.Activate();
        var handle2 = activator.Activate();
        handle1.Dispose();

        // Assert - should still be activated (one reference remaining)
        Assert.Single(states); // Only one Activated
        Assert.True(activator.IsActivated);

        // Act - dispose second handle
        handle2.Dispose();

        // Assert - now deactivated
        Assert.Equal(2, states.Count);
        Assert.Equal(ActivationState.Deactivated, states[1]);
        Assert.False(activator.IsActivated);
    }

    [Fact]
    public void Dispose_CompletesActivationObservable()
    {
        // Arrange
        var activator = new ViewModelActivator();
        var completed = false;
        using var sub = activator.Activation.Subscribe(
            _ => { },
            _ => completed = true);

        // Act
        activator.Dispose();

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public void ActivateAfterDispose_ReturnsEmptyDisposable()
    {
        // Arrange
        var activator = new ViewModelActivator();
        activator.Dispose();

        // Act
        var handle = activator.Activate();

        // Assert
        Assert.False(activator.IsActivated);
        handle.Dispose(); // Should not throw
    }

    [Fact]
    public void DeactivateWithoutActivation_DoesNotThrow()
    {
        // Arrange
        var activator = new ViewModelActivator();

        // Act & Assert - should not throw
        activator.Deactivate();
        Assert.False(activator.IsActivated);
    }

    [Fact]
    public void DoubleDisposeOfHandle_OnlyDeactivatesOnce()
    {
        // Arrange
        var activator = new ViewModelActivator();
        var deactivateCount = 0;
        using var sub = activator.Activation.Subscribe(s =>
        {
            if (s == ActivationState.Deactivated)
            {
                deactivateCount++;
            }
        });

        // Act
        var handle = activator.Activate();
        handle.Dispose();
        handle.Dispose(); // Second dispose

        // Assert
        Assert.Equal(1, deactivateCount);
    }
}
