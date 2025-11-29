using R3;
using R3Ext.Activation;
using Xunit;

namespace R3Ext.Tests.Activation;

public class ActivatableViewExtensionsTests
{
    [Fact]
    public void WhenActivated_ExecutesBlockOnActivation()
    {
        // Arrange
        var view = new TestActivatableView();
        var blockExecuted = false;

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => blockExecuted = true);
        view.Activate();

        // Assert
        Assert.True(blockExecuted);
    }

    [Fact]
    public void WhenActivated_DisposesDisposableBagOnDeactivation()
    {
        // Arrange
        var view = new TestActivatableView();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        // Act
        using var sub = view.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));
        view.Activate();
        Assert.False(disposed);

        view.Deactivate();

        // Assert
        Assert.True(disposed);
    }

    [Fact]
    public void WhenActivated_ReactivatesOnSecondActivation()
    {
        // Arrange
        var view = new TestActivatableView();
        var activationCount = 0;

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => activationCount++);
        view.Activate();
        view.Deactivate();
        view.Activate();

        // Assert
        Assert.Equal(2, activationCount);
    }

    [Fact]
    public void WhenActivated_DisposingSubscriptionCleansUp()
    {
        // Arrange
        var view = new TestActivatableView();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        // Act
        var sub = view.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));
        view.Activate();
        sub.Dispose();

        // Assert
        Assert.True(disposed);
    }

    [Fact]
    public void WhenActivated_WithIViewFor_AutoActivatesViewModel()
    {
        // Arrange
        var viewModel = new TestActivatableViewModel();
        var view = new TestViewFor<TestActivatableViewModel> { ViewModel = viewModel };
        var vmActivated = false;

        using var vmSub = viewModel.Activator.Activation.Subscribe(s =>
        {
            if (s == ActivationState.Activated)
            {
                vmActivated = true;
            }
        });

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => { });
        view.Activate();

        // Assert
        Assert.True(vmActivated);
        Assert.True(viewModel.Activator.IsActivated);
    }

    [Fact]
    public void WhenActivated_WithIViewFor_DeactivatesViewModelOnDeactivation()
    {
        // Arrange
        var viewModel = new TestActivatableViewModel();
        var view = new TestViewFor<TestActivatableViewModel> { ViewModel = viewModel };

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => { });
        view.Activate();
        view.Deactivate();

        // Assert
        Assert.False(viewModel.Activator.IsActivated);
    }

    [Fact]
    public void WhenAttached_ExecutesBlockOnActivation()
    {
        // Arrange
        var view = new TestActivatableView();
        var blockExecuted = false;

        // Act
        using var sub = view.WhenAttached((ref DisposableBag _) => blockExecuted = true);
        view.Activate();

        // Assert
        Assert.True(blockExecuted);
    }

    /// <summary>
    /// Test implementation of IActivatableView.
    /// </summary>
    private sealed class TestActivatableView : IActivatableView, IDisposable
    {
        private readonly Subject<ActivationState> _activation = new();

        public Observable<ActivationState> Activation => _activation;

        public void Activate() => _activation.OnNext(ActivationState.Activated);

        public void Deactivate() => _activation.OnNext(ActivationState.Deactivated);

        public void Dispose() => _activation.Dispose();
    }

    /// <summary>
    /// Test implementation of IViewFor.
    /// </summary>
    private sealed class TestViewFor<TViewModel> : IViewFor<TViewModel>, IDisposable
        where TViewModel : class
    {
        private readonly Subject<ActivationState> _activation = new();

        public Observable<ActivationState> Activation => _activation;

        public TViewModel? ViewModel { get; set; }

        public void Activate() => _activation.OnNext(ActivationState.Activated);

        public void Deactivate() => _activation.OnNext(ActivationState.Deactivated);

        public void Dispose() => _activation.Dispose();
    }

    /// <summary>
    /// Test implementation of IActivatableViewModel.
    /// </summary>
    private sealed class TestActivatableViewModel : IActivatableViewModel
    {
        public ViewModelActivator Activator { get; } = new();
    }
}
