using R3;
using R3Ext.Activation;
using Xunit;

namespace R3Ext.Tests.Activation;

public class ActivatableViewModelExtensionsTests
{
    [Fact]
    public void WhenActivated_ExecutesBlockOnActivation()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var blockExecuted = false;

        using var sub = viewModel.WhenActivated((ref DisposableBag _) => blockExecuted = true);

        // Act
        using var handle = viewModel.Activator.Activate();

        // Assert
        Assert.True(blockExecuted);
    }

    [Fact]
    public void WhenActivated_DisposesDisposableBagOnDeactivation()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        using var sub = viewModel.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));

        // Act
        var handle = viewModel.Activator.Activate();
        Assert.False(disposed);

        handle.Dispose();

        // Assert
        Assert.True(disposed);
    }

    [Fact]
    public void WhenActivated_MultipleActivations_ExecutesBlockEachTime()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var activationCount = 0;

        using var sub = viewModel.WhenActivated((ref DisposableBag _) => activationCount++);

        // Act
        var handle1 = viewModel.Activator.Activate();
        handle1.Dispose();
        var handle2 = viewModel.Activator.Activate();
        handle2.Dispose();

        // Assert
        Assert.Equal(2, activationCount);
    }

    [Fact]
    public void WhenActivated_DisposingSubscription_CleansUpCurrentBag()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        var sub = viewModel.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));

        // Act
        using var handle = viewModel.Activator.Activate();
        sub.Dispose();

        // Assert
        Assert.True(disposed);
    }

    [Fact]
    public void WhenActivated_CanNestWithViewActivation()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var innerSubscriptionCreated = false;
        var innerSubscriptionDisposed = false;

        using var sub = viewModel.WhenActivated((ref DisposableBag bag) =>
        {
            innerSubscriptionCreated = true;
            Disposable.Create(() => innerSubscriptionDisposed = true).AddTo(ref bag);
        });

        // Act - Activate
        var handle = viewModel.Activator.Activate();
        Assert.True(innerSubscriptionCreated);
        Assert.False(innerSubscriptionDisposed);

        // Act - Deactivate
        handle.Dispose();

        // Assert
        Assert.True(innerSubscriptionDisposed);
    }

    private sealed class TestViewModel : IActivatableViewModel
    {
        public ViewModelActivator Activator { get; } = new();
    }
}
