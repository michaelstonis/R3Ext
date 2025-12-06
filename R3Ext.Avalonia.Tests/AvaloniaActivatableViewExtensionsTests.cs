// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using R3;
using R3Ext.Activation;
using Xunit;

namespace R3Ext.Avalonia.Tests;

/// <summary>
/// Tests for AvaloniaActivatableViewExtensions.
/// </summary>
public class AvaloniaActivatableViewExtensionsTests
{
    [AvaloniaFact]
    public void WhenActivated_ExecutesBlockOnVisualTreeAttachment()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var blockExecuted = false;

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => blockExecuted = true);
        window.Content = view;
        window.Show();

        // Assert
        Assert.True(blockExecuted);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_DisposesDisposableBagOnDeactivation()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        // Act
        using var sub = view.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));
        window.Content = view;
        window.Show();

        Assert.False(disposed);

        // Detach from visual tree
        window.Content = null;

        // Assert
        Assert.True(disposed);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_ReactivatesOnSecondAttachment()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var activationCount = 0;

        // Act
        using var sub = view.WhenActivated((ref DisposableBag _) => activationCount++);
        window.Content = view;
        window.Show();
        window.Content = null;
        window.Content = view;

        // Assert
        Assert.Equal(2, activationCount);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_DisposingSubscriptionCleansUp()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        // Act
        var sub = view.WhenActivated((ref DisposableBag bag) => testDisposable.AddTo(ref bag));
        window.Content = view;
        window.Show();
        sub.Dispose();

        // Assert
        Assert.True(disposed);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_AutoActivatesViewModel()
    {
        // Arrange
        var viewModel = new TestActivatableViewModel();
        var view = new TestAvaloniaViewFor<TestActivatableViewModel> { ViewModel = viewModel };
        var window = new Window();
        var vmActivated = false;

        using var vmSub = viewModel.Activator.Activation.Subscribe(s =>
        {
            if (s == ActivationState.Activated)
            {
                vmActivated = true;
            }
        });

        // Act
        using var sub = view.WhenActivated<TestAvaloniaViewFor<TestActivatableViewModel>, TestActivatableViewModel>(
            (ref DisposableBag _) => { });
        window.Content = view;
        window.Show();

        // Assert
        Assert.True(vmActivated);
        Assert.True(viewModel.Activator.IsActivated);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_DeactivatesViewModelOnDetachment()
    {
        // Arrange
        var viewModel = new TestActivatableViewModel();
        var view = new TestAvaloniaViewFor<TestActivatableViewModel> { ViewModel = viewModel };
        var window = new Window();

        // Act
        using var sub = view.WhenActivated<TestAvaloniaViewFor<TestActivatableViewModel>, TestActivatableViewModel>(
            (ref DisposableBag _) => { });
        window.Content = view;
        window.Show();
        window.Content = null;

        // Assert
        Assert.False(viewModel.Activator.IsActivated);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenAttached_ExecutesBlockOnVisualTreeAttachment()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var blockExecuted = false;

        // Act
        using var sub = view.WhenAttached((ref DisposableBag _) => blockExecuted = true);
        window.Content = view;
        window.Show();

        // Assert
        Assert.True(blockExecuted);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenAttached_DisposesDisposableBagOnDetachment()
    {
        // Arrange
        var window = new Window();
        var view = new TestAvaloniaView();
        var disposed = false;
        var testDisposable = Disposable.Create(() => disposed = true);

        // Act
        using var sub = view.WhenAttached((ref DisposableBag bag) => testDisposable.AddTo(ref bag));
        window.Content = view;
        window.Show();

        Assert.False(disposed);

        window.Content = null;

        // Assert
        Assert.True(disposed);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenAttached_AutoAttachesViewModel()
    {
        // Arrange
        var viewModel = new TestAttachableViewModel();
        var view = new TestAvaloniaViewFor<TestAttachableViewModel> { ViewModel = viewModel };
        var window = new Window();
        var vmAttached = false;

        // ViewModelAttacher.Activation emits ActivationState.Activated when attached
        using var vmSub = viewModel.Attacher.Activation.Subscribe(s =>
        {
            if (s == ActivationState.Activated)
            {
                vmAttached = true;
            }
        });

        // Act
        using var sub = view.WhenAttached<TestAvaloniaViewFor<TestAttachableViewModel>, TestAttachableViewModel>(
            (ref DisposableBag _) => { });
        window.Content = view;
        window.Show();

        // Assert
        Assert.True(vmAttached);
        Assert.True(viewModel.Attacher.IsAttached);

        window.Close();
    }

    [AvaloniaFact]
    public void WhenActivated_ThrowsOnNullView()
    {
        // Arrange
        TestAvaloniaView? view = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            view!.WhenActivated((ref DisposableBag _) => { }));
    }

    [AvaloniaFact]
    public void WhenActivated_ThrowsOnNullBlock()
    {
        // Arrange
        var view = new TestAvaloniaView();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            view.WhenActivated(null!));
    }

    [AvaloniaFact]
    public void WhenAttached_ThrowsOnNullView()
    {
        // Arrange
        TestAvaloniaView? view = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            view!.WhenAttached((ref DisposableBag _) => { }));
    }

    [AvaloniaFact]
    public void WhenAttached_ThrowsOnNullBlock()
    {
        // Arrange
        var view = new TestAvaloniaView();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            view.WhenAttached(null!));
    }
}
